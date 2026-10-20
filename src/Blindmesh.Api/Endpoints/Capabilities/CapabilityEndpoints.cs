using System.Globalization;
using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Capabilities;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Runtime;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Capabilities;

/// <summary>
/// The capability endpoints of the product. The visibility of a capability is
/// enforced on every read, list, detail and command, and the actor policy is
/// checked again on every command. No body of this module holds a credential:
/// a field outside the allowed ones is refused without a store and without an
/// echo of its value.
/// </summary>
public static class CapabilityEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int DefaultLimit = 20;

    private const int MaxLimit = 100;

    private static readonly string[] CreateFields = ["kind", "label", "tags", "visibility", "available", "metadata"];

    private static readonly string[] UpdateFields =
        ["expected_version", "kind", "label", "tags", "visibility", "available", "metadata"];

    private static readonly string[] WithdrawFields = ["expected_version"];

    private static readonly string[] EligibilityFields = ["conditions"];

    public static void MapCapabilityEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/capabilities", HandleListAsync)
            .WithName("ListCapabilities")
            .WithSummary("List the capabilities the caller may see.")
            .Produces<PagedResult<CapabilityView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);

        app.MapPost("/v1/capabilities", HandleCreateAsync)
            .WithName("CreateCapability")
            .WithSummary("Declare a capability of the signed in contributor.")
            .Accepts<CreateCapabilityRequest>("application/json")
            .Produces<CapabilityView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/capabilities/{id}", HandleGetAsync)
            .WithName("ReadCapability")
            .WithSummary("Read the committed state of a capability.")
            .Produces<CapabilityView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/v1/capabilities/{id}", HandleUpdateAsync)
            .WithName("UpdateCapability")
            .WithSummary("Save the content or the visibility of a capability.")
            .Accepts<UpdateCapabilityRequest>("application/json")
            .Produces<CapabilityView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/capabilities/{id}/withdraw", HandleWithdrawAsync)
            .WithName("WithdrawCapability")
            .WithSummary("Withdraw a capability of the signed in contributor.")
            .Accepts<WithdrawCapabilityRequest>("application/json")
            .Produces<CapabilityView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/capabilities/eligibility", HandleEvaluateAsync)
            .WithName("EvaluateCapabilityEligibility")
            .WithSummary("Evaluate a set of hard conditions against the declarations of the caller.")
            .Accepts<EvaluateEligibilityRequest>("application/json")
            .Produces<CapabilityEligibilityView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/capabilities/tasks/{task_id}/eligibility", HandleEvaluateForTaskAsync)
            .WithName("EvaluateTaskEligibility")
            .WithSummary("Evaluate the hard conditions of a published task against the declarations of the caller.")
            .Produces<CapabilityEligibilityView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        var rawCursor = context.Request.Query["cursor"].ToString();
        CapabilityCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = CapabilityCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    CapabilityErrorCodes.BadRequest,
                    "The cursor of the list is not valid.");
            }
        }

        var limit = ReadLimit(context.Request.Query["limit"].ToString());
        var scope = context.Request.Query["scope"].ToString().Trim().ToLowerInvariant();

        if (scope == "own")
        {
            if (!actor.IsAuthenticated)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    CapabilityErrorCodes.SessionRequired,
                    "A signed in contributor is required to read the own capabilities.");
            }

            var own = await capabilityService.ListOwnedAsync(actor, limit, cursor, cancellationToken);

            return Results.Ok(own);
        }

        var page = await capabilityService.ListAsync(actor, limit, cursor, cancellationToken);

        return Results.Ok(page);
    }

    private static async Task<IResult> HandleCreateAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<JsonOptions> jsonOptions,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await CapabilityRequestReader.ReadAsync<CreateCapabilityRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            CreateFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CapabilityErrorCodes.BadRequest,
                "The request body is not valid. It holds only the fields kind, label, tags, visibility, available and metadata.");
        }

        var request = body.Value;

        if (!TryParseKind(request.Kind, out var kind))
        {
            return Unprocessable(
                context,
                "kind",
                "The kind must be account, device, skill, region, network or role.");
        }

        if (!TryParseVisibility(request.Visibility, out var visibility))
        {
            return Unprocessable(
                context,
                "visibility",
                "The visibility must be public or private.");
        }

        var input = new CapabilityInput
        {
            Kind = kind,
            Label = request.Label ?? string.Empty,
            Tags = request.Tags ?? [],
            Visibility = visibility,
            Available = request.Available ?? true,
            Metadata = request.Metadata ?? new Dictionary<string, string>(StringComparer.Ordinal),
        };

        var key = context.Request.Headers[IdempotencyHeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(key) && !IdempotencyKey.IsValid(key))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ApiErrorWriter.InvalidIdempotencyKey,
                $"The {IdempotencyHeaderName} header must be 16 to 128 printable ASCII characters.",
                new Dictionary<string, string[]>
                {
                    [IdempotencyHeaderName] = ["Use 16 to 128 printable ASCII characters."],
                });
        }

        var payloadHash = PayloadHasher.Compute(body.Raw);
        var idempotencyScope = $"{actor!.UserId}|capability.create";

        if (!string.IsNullOrWhiteSpace(key))
        {
            var existing = await idempotencyStore.FindAsync(idempotencyScope, key, cancellationToken);

            if (existing is not null)
            {
                return ReplayOrReject(context, existing, payloadHash);
            }
        }

        ICommandTransactionScope? transaction = await transactionFactory.BeginAsync(cancellationToken);

        try
        {
            var result = await capabilityService.CreateAsync(actor!, input, cancellationToken);

            if (result.Status != CapabilityCommandStatus.Created)
            {
                return ToResult(context, result);
            }

            var responseBody = JsonSerializer.Serialize(result.Capability, jsonOptions.Value.SerializerOptions);

            if (!string.IsNullOrWhiteSpace(key))
            {
                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = StatusCodes.Status201Created,
                        ResponseBody = responseBody,
                        CreatedAt = clock.UtcNow,
                        ExpiresAt = clock.UtcNow.AddDays(7),
                    },
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status201Created);
        }
        catch (DbUpdateException exception) when (PostgresErrors.IsUniqueViolation(exception, "ux_idempotency_records_scope_key"))
        {
            // A concurrent request stored the same key first. Its committed
            // response is the only truthful answer for this key.
            await transaction.DisposeAsync();
            transaction = null;

            using var commandScope = scopeFactory.CreateScope();
            var store = commandScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
            var stored = await store.FindAsync(idempotencyScope, key, cancellationToken);

            if (stored is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status409Conflict,
                    ApiErrorWriter.Conflict,
                    "The command is already running with this key.");
            }

            return ReplayOrReject(context, stored, payloadHash);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        if (!TryReadCapabilityId(context, out var capabilityId))
        {
            return NotFound(context);
        }

        var actor = await ReadActorAsync(context, guard, cancellationToken);
        var result = await capabilityService.GetAsync(actor, capabilityId, cancellationToken);

        return ToResult(context, result);
    }

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadCapabilityId(context, out var capabilityId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await CapabilityRequestReader.ReadAsync<UpdateCapabilityRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            UpdateFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CapabilityErrorCodes.BadRequest,
                "The request body is not valid. It holds only the fields expected_version, kind, label, tags, visibility, available and metadata.");
        }

        var request = body.Value;

        if (request.ExpectedVersion is null)
        {
            return Unprocessable(
                context,
                "expected_version",
                "The version of the capability that was read is required.");
        }

        CapabilityKind? kind = null;

        if (request.Kind is not null)
        {
            if (!TryParseKind(request.Kind, out var parsed))
            {
                return Unprocessable(
                    context,
                    "kind",
                    "The kind must be account, device, skill, region, network or role.");
            }

            kind = parsed;
        }

        CapabilityVisibility? visibility = null;

        if (request.Visibility is not null)
        {
            if (!TryParseVisibility(request.Visibility, out var parsed))
            {
                return Unprocessable(
                    context,
                    "visibility",
                    "The visibility must be public or private.");
            }

            visibility = parsed;
        }

        var patch = new CapabilityPatch
        {
            Kind = kind,
            Label = request.Label,
            Tags = request.Tags,
            Visibility = visibility,
            Available = request.Available,
            Metadata = request.Metadata,
        };

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => capabilityService.UpdateAsync(actor!, capabilityId, patch, request.ExpectedVersion!.Value, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> HandleWithdrawAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadCapabilityId(context, out var capabilityId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await CapabilityRequestReader.ReadAsync<WithdrawCapabilityRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            WithdrawFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CapabilityErrorCodes.BadRequest,
                "The request body is not valid. It holds only the field expected_version.");
        }

        if (body.Value.ExpectedVersion is null)
        {
            return Unprocessable(
                context,
                "expected_version",
                "The version of the capability that was read is required.");
        }

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => capabilityService.WithdrawAsync(actor!, capabilityId, body.Value!.ExpectedVersion!.Value, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> HandleEvaluateAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await CapabilityRequestReader.ReadAsync<EvaluateEligibilityRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            EligibilityFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                CapabilityErrorCodes.BadRequest,
                "The request body is not valid. It holds only the field conditions.");
        }

        var conditions = new List<CapabilityHardCondition>(body.Value.Conditions?.Length ?? 0);

        foreach (var condition in body.Value.Conditions ?? [])
        {
            if (!TryParseKind(condition.Kind, out var kind) ||
                !CapabilityHardCondition.MatchableKinds.Contains(kind))
            {
                return Unprocessable(
                    context,
                    "conditions",
                    "A condition uses the kind region, device or role.");
            }

            conditions.Add(new CapabilityHardCondition(kind, condition.Value ?? string.Empty));
        }

        var result = await capabilityService.EvaluateAsync(actor!, conditions, cancellationToken);

        return ToEvaluationResult(context, result);
    }

    private static async Task<IResult> HandleEvaluateForTaskAsync(
        HttpContext context,
        CapabilityService capabilityService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        if (!TryReadTaskId(context, out var taskId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await capabilityService.EvaluateForTaskAsync(actor!, taskId, cancellationToken);

        return ToEvaluationResult(context, result);
    }

    /// <summary>
    /// Runs a command inside the transaction of the request. A refused command
    /// never commits, so the data of the capability stays as it was.
    /// </summary>
    private static async Task<IResult> RunCommandAsync(
        HttpContext context,
        ICommandTransactionFactory transactionFactory,
        Func<Task<CapabilityCommandResult>> command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var result = await command();

        if (result.Status is not (CapabilityCommandStatus.Succeeded or CapabilityCommandStatus.Created))
        {
            return ToResult(context, result);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(result.Capability);
    }

    private static async Task<CapabilityActor> ReadActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return CapabilityActor.Anonymous;
        }

        if (session is null || !session.IsAuthenticated)
        {
            return CapabilityActor.Anonymous;
        }

        return new CapabilityActor(session.UserId, session.Username);
    }

    /// <summary>
    /// The actor of a read that needs a signed in contributor. A safe request
    /// carries no CSRF token, so the session alone opens the read.
    /// </summary>
    private static async Task<(CapabilityActor? Actor, IResult? Refusal)> ReadSignedInActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CapabilityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CapabilityErrorCodes.SessionRequired,
                "A signed in contributor is required for this request."));
        }

        return (new CapabilityActor(session.UserId, session.Username), null);
    }

    private static async Task<(CapabilityActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CapabilityErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                CapabilityErrorCodes.SessionRequired,
                "A signed in contributor is required for this request."));
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                CapabilityErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (new CapabilityActor(session.UserId, session.Username), null);
    }

    private static IResult ToResult(HttpContext context, CapabilityCommandResult result) => result.Status switch
    {
        CapabilityCommandStatus.Succeeded => Results.Ok(result.Capability),
        CapabilityCommandStatus.Created => Results.Ok(result.Capability),
        CapabilityCommandStatus.NotFound => NotFound(context),
        CapabilityCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            CapabilityErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this contributor."),
        CapabilityCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            CapabilityErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        CapabilityCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            CapabilityErrorCodes.VersionConflict,
            result.Message ?? "The capability was changed somewhere else.",
            result.CurrentVersion is null
                ? null
                : new Dictionary<string, string[]>
                {
                    ["expected_version"] = [$"The current version is {result.CurrentVersion.Value}."],
                }),
        CapabilityCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            CapabilityErrorCodes.StateConflict,
            result.Message ?? "The state of the capability does not accept this command."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The capability could not be changed."),
    };

    private static IResult ToEvaluationResult(HttpContext context, CapabilityEvaluationResult result) => result.Status switch
    {
        CapabilityEvaluationStatus.Evaluated => Results.Ok(result.Evaluation),
        CapabilityEvaluationStatus.NotFound => NotFound(context),
        CapabilityEvaluationStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status401Unauthorized,
            CapabilityErrorCodes.SessionRequired,
            result.Message ?? "A signed in contributor is required for this request."),
        CapabilityEvaluationStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            CapabilityErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The eligibility could not be evaluated."),
    };

    private static IResult Unprocessable(HttpContext context, string field, string message) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            CapabilityErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            new Dictionary<string, string[]>
            {
                [field] = [message],
            });

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            CapabilityErrorCodes.NotFound,
            "The capability does not exist.");

    private static bool TryReadCapabilityId(HttpContext context, out Guid capabilityId) =>
        Guid.TryParse(context.Request.RouteValues["id"] as string, out capabilityId);

    private static bool TryReadTaskId(HttpContext context, out Guid taskId) =>
        Guid.TryParse(context.Request.RouteValues["task_id"] as string, out taskId);

    private static int ReadLimit(string? raw)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
        {
            return DefaultLimit;
        }

        if (limit < 1)
        {
            return DefaultLimit;
        }

        return Math.Min(limit, MaxLimit);
    }

    private static bool TryParseKind(string? value, out CapabilityKind kind)
    {
        kind = CapabilityKind.Skill;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "account":
                kind = CapabilityKind.Account;
                return true;
            case "device":
                kind = CapabilityKind.Device;
                return true;
            case "skill":
                kind = CapabilityKind.Skill;
                return true;
            case "region":
                kind = CapabilityKind.Region;
                return true;
            case "network":
                kind = CapabilityKind.Network;
                return true;
            case "role":
                kind = CapabilityKind.Role;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseVisibility(string? value, out CapabilityVisibility visibility)
    {
        visibility = CapabilityVisibility.Private;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "public":
                visibility = CapabilityVisibility.Public;
                return true;
            case "private":
                visibility = CapabilityVisibility.Private;
                return true;
            default:
                return false;
        }
    }

    private static IResult ReplayOrReject(HttpContext context, IdempotencyRecord record, string payloadHash)
    {
        if (!string.Equals(record.PayloadHash, payloadHash, StringComparison.Ordinal))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status409Conflict,
                ApiErrorWriter.IdempotencyKeyReuse,
                "The idempotency key was already used with a different payload.");
        }

        return Results.Text(record.ResponseBody, "application/json", statusCode: record.StatusCode);
    }
}
