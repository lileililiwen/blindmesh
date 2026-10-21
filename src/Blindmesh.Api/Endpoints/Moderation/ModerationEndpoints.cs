using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Moderation;
using Blindmesh.Application.Runtime;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Moderation;

/// <summary>
/// The moderation endpoints of the product. A report belongs to the reporter
/// and keeps its body away from everybody else. A case belongs to the parties
/// of the arbitration and to the moderation staff, and nobody else finds it.
/// Every mutating command writes its idempotency record in the same
/// transaction as the state of the case and as the ledger row of the escrow.
/// </summary>
public static class ModerationEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int MaxAttempts = 3;

    private static readonly string[] ReportFields = ["reason", "subject_type", "subject_id"];

    private static readonly string[] DisputeFields = ["reason", "expected_version"];

    private static readonly string[] EvidenceFields = ["note", "expected_version"];

    private static readonly string[] ResolveFields = ["award_base", "reason", "bonus", "expected_version"];

    private static readonly string[] CloseFields = ["reason", "expected_version"];

    private static readonly string[] BanFields = ["reason", "until"];

    public static void MapModerationEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/reports", HandleCreateReportAsync)
            .WithName("CreateReport")
            .WithSummary("Write a report and read the receipt of it back.")
            .Accepts<ReportBody>("application/json")
            .Produces<ReportReceiptView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/reports/{id}", HandleGetReportAsync)
            .WithName("GetReport")
            .WithSummary("Read the receipt of a report of the reporter.")
            .Produces<ReportReceiptView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/admin/reports", HandleListReportsAsync)
            .WithName("ListReports")
            .WithSummary("List the reports of the moderation staff.")
            .Produces<IReadOnlyList<ReportView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapPost("/v1/submissions/{id}/disputes", HandleOpenDisputeAsync)
            .WithName("OpenDispute")
            .WithSummary("Open the dispute of a seat and freeze its settlement.")
            .Accepts<DisputeBody>("application/json")
            .Produces<DisputeView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/disputes/{id}", HandleGetDisputeAsync)
            .WithName("GetDispute")
            .WithSummary("Read the case of an arbitration with its trail.")
            .Produces<DisputeView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/admin/disputes", HandleListDisputesAsync)
            .WithName("ListDisputes")
            .WithSummary("List the cases of the moderation staff.")
            .Produces<IReadOnlyList<DisputeSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapPost("/v1/admin/disputes/{id}/request-evidence", HandleRequestEvidenceAsync)
            .WithName("RequestDisputeEvidence")
            .WithSummary("Ask the parties of a case for more evidence.")
            .Accepts<EvidenceBody>("application/json")
            .Produces<DisputeView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/admin/disputes/{id}/resolve", HandleResolveDisputeAsync)
            .WithName("ResolveDispute")
            .WithSummary("Decide a case and move the escrow of the seat.")
            .Accepts<ResolveBody>("application/json")
            .Produces<DisputeView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/admin/disputes/{id}/close", HandleCloseDisputeAsync)
            .WithName("CloseDispute")
            .WithSummary("Close a case whose decision stands.")
            .Accepts<CloseBody>("application/json")
            .Produces<DisputeView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/disputes/{id}/appeal", HandleAppealDisputeAsync)
            .WithName("AppealDispute")
            .WithSummary("Appeal the decision of a case once.")
            .Accepts<DisputeBody>("application/json")
            .Produces<DisputeView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/admin/users/{id}/ban", HandleBanUserAsync)
            .WithName("BanUser")
            .WithSummary("Ban an account and refuse its requests.")
            .Accepts<BanBody>("application/json")
            .Produces<BanView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> HandleCreateReportAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ApiRequestReader.ReadAsync<ReportBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            ReportFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new OpenReportInput(
            body.Value.Reason ?? string.Empty,
            body.Value.SubjectType,
            body.Value.SubjectId);

        return await RunCommandAsync(
            context,
            "moderation:report",
            PayloadHasher.Compute(body.Raw),
            key => moderation.OpenReportAsync(actor!, input, cancellationToken),
            result => result.Receipt,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleGetReportAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var reportId))
        {
            return NotFound(context);
        }

        var result = await moderation.GetReportAsync(actor!, reportId, cancellationToken);

        if (result.Status != ModerationCommandStatus.Succeeded)
        {
            return ToResult(context, result);
        }

        return Results.Ok(result.Receipt is not null ? result.Receipt : result.Report);
    }

    private static async Task<IResult> HandleListReportsAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await moderation.ListReportsAsync(actor!, cancellationToken);

        if (result.Status != ModerationCommandStatus.Succeeded)
        {
            return ToResult(context, result);
        }

        return Results.Ok(result.Reports ?? []);
    }

    private static async Task<IResult> HandleOpenDisputeAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var submissionId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<DisputeBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            DisputeFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new OpenDisputeInput(body.Value.Reason ?? string.Empty, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"moderation:dispute:{submissionId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.OpenDisputeAsync(actor!, submissionId, input, cancellationToken),
            result => result.Dispute,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleGetDisputeAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var disputeId))
        {
            return NotFound(context);
        }

        var result = await moderation.GetDisputeAsync(actor!, disputeId, cancellationToken);

        if (result.Status != ModerationCommandStatus.Succeeded)
        {
            return ToResult(context, result);
        }

        return Results.Ok(result.Dispute);
    }

    private static async Task<IResult> HandleListDisputesAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await moderation.ListDisputesAsync(actor!, cancellationToken);

        if (result.Status != ModerationCommandStatus.Succeeded)
        {
            return ToResult(context, result);
        }

        return Results.Ok(result.Disputes ?? []);
    }

    private static async Task<IResult> HandleRequestEvidenceAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var disputeId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<EvidenceBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            EvidenceFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new RequestEvidenceInput(body.Value.Note ?? string.Empty, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"moderation:request-evidence:{disputeId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.RequestEvidenceAsync(actor!, disputeId, input, cancellationToken),
            result => result.Dispute,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleResolveDisputeAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var disputeId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<ResolveBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            ResolveFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new ResolveDisputeInput(
            body.Value.AwardBase,
            body.Value.Reason ?? string.Empty,
            body.Value.ExpectedVersion,
            body.Value.Bonus ?? 0);

        return await RunCommandAsync(
            context,
            $"moderation:resolve:{disputeId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.ResolveDisputeAsync(actor!, disputeId, input, key, cancellationToken),
            result => result.Dispute,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleCloseDisputeAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var disputeId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<CloseBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            CloseFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new CloseDisputeInput(body.Value.Reason, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"moderation:close:{disputeId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.CloseDisputeAsync(actor!, disputeId, input, cancellationToken),
            result => result.Dispute,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleAppealDisputeAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var disputeId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<DisputeBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            DisputeFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new AppealDisputeInput(body.Value.Reason ?? string.Empty, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"moderation:appeal:{disputeId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.AppealDisputeAsync(actor!, disputeId, input, cancellationToken),
            result => result.Dispute,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleBanUserAsync(
        HttpContext context,
        ModerationService moderation,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadRouteId(context, "id", out var userId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<BanBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            BanFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new BanUserInput(body.Value.Reason ?? string.Empty, body.Value.Until);

        return await RunCommandAsync(
            context,
            $"moderation:ban:{userId:D}",
            PayloadHasher.Compute(body.Raw),
            key => moderation.BanUserAsync(actor!, userId, input, cancellationToken),
            result => result.Ban,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<(ModerationActor? Actor, IResult? Refusal)> ReadSignedInActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ModerationErrorCodes.SessionRequired,
                "A signed in account is required."));
        }

        return (new ModerationActor(session.UserId, session.Username, session.Role), null);
    }

    private static async Task<(ModerationActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return (null, refusal);
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                ModerationErrorCodes.Forbidden,
                "The request does not carry the CSRF token of the session."));
        }

        return (actor, null);
    }

    private static async Task<IResult> RunCommandAsync(
        HttpContext context,
        string idempotencyScope,
        string payload,
        Func<string?, Task<ModerationCommandResult>> command,
        Func<ModerationCommandResult, object?> body,
        IIdempotencyStore idempotencyStore,
        ICommandTransactionFactory transactionFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<JsonOptions> jsonOptions,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var key = context.Request.Headers[IdempotencyHeaderName].ToString();

        if (!IdempotencyKey.IsValid(key))
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

        var payloadHash = PayloadHasher.Compute(payload);
        var existing = await idempotencyStore.FindAsync(idempotencyScope, key, cancellationToken);

        if (existing is not null)
        {
            return ReplayOrReject(context, existing, payloadHash);
        }

        for (var attempt = 1; ; attempt++)
        {
            ICommandTransactionScope? transaction = null;

            try
            {
                transaction = await transactionFactory.BeginAsync(cancellationToken);

                var result = await command(key);

                if (result.Status is not (ModerationCommandStatus.Succeeded or ModerationCommandStatus.Created))
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    return ToResult(context, result);
                }

                var responseBody = JsonSerializer.Serialize(
                    body(result),
                    jsonOptions.Value.SerializerOptions);

                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = result.Status == ModerationCommandStatus.Created
                            ? StatusCodes.Status201Created
                            : StatusCodes.Status200OK,
                        ResponseBody = responseBody,
                        CreatedAt = clock.UtcNow,
                        ExpiresAt = clock.UtcNow.AddDays(7),
                    },
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Results.Text(
                    responseBody,
                    "application/json",
                    statusCode: result.Status == ModerationCommandStatus.Created
                        ? StatusCodes.Status201Created
                        : StatusCodes.Status200OK);
            }
            catch (DbUpdateException exception) when (
                PostgresErrors.IsUniqueViolation(exception, "ux_idempotency_records_scope_key"))
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }

                using var commandScope = scopeFactory.CreateScope();
                var store = commandScope.ServiceProvider.GetRequiredService<IIdempotencyStore>();
                var stored = await store.FindAsync(idempotencyScope, key, cancellationToken);

                if (stored is null)
                {
                    return ApiErrorWriter.Create(
                        context,
                        StatusCodes.Status409Conflict,
                        ModerationErrorCodes.Conflict,
                        "The command is already running with this key.");
                }

                return ReplayOrReject(context, stored, payloadHash);
            }
            catch (Exception exception) when (
                attempt < MaxAttempts && PostgresErrors.IsRetryableConflict(exception))
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                    transaction = null;
                }
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }
    }

    private static IResult ToResult(HttpContext context, ModerationCommandResult result) => result.Status switch
    {
        ModerationCommandStatus.Succeeded => Results.Ok(Body(result)),
        ModerationCommandStatus.Created => Results.Ok(Body(result)),
        ModerationCommandStatus.NotFound => NotFound(context),
        ModerationCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            ModerationErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this account."),
        ModerationCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            ModerationErrorCodes.Unprocessable,
            result.Message ?? "Some fields of the request are not valid.",
            result.FieldErrors),
        ModerationCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ModerationErrorCodes.VersionConflict,
            result.Message ?? "The case changed somewhere else."),
        ModerationCommandStatus.InvalidTransition => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ModerationErrorCodes.InvalidTransition,
            result.Message ?? "The requested transition is not allowed."),
        ModerationCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ModerationErrorCodes.StateConflict,
            result.Message ?? "The state of the case does not accept this command."),
        ModerationCommandStatus.Conflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ModerationErrorCodes.Conflict,
            result.Message ?? "The command conflicts with the state of the case."),
        ModerationCommandStatus.NotReversible => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ModerationErrorCodes.ArbitrationNotReversible,
            result.Message ?? "The award of the case cannot be reversed."),
        ModerationCommandStatus.Unavailable => ApiErrorWriter.Create(
            context,
            StatusCodes.Status503ServiceUnavailable,
            ApiErrorWriter.ServiceUnavailable,
            "The service is not reachable."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The case could not be changed."),
    };

    private static object? Body(ModerationCommandResult result)
    {
        if (result.Dispute is not null)
        {
            return result.Dispute;
        }

        if (result.Receipt is not null)
        {
            return result.Receipt;
        }

        if (result.Report is not null)
        {
            return result.Report;
        }

        return result.Ban;
    }

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            ModerationErrorCodes.NotFound,
            "The resource does not exist.");

    private static IResult BadRequest(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status400BadRequest,
            ModerationErrorCodes.BadRequest,
            "The request is not valid.");

    /// <summary>
    /// Answers with the committed response of an earlier command with the same
    /// key, or refuses the reuse of the key with another payload.
    /// </summary>
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

    private static bool TryReadRouteId(HttpContext context, string name, out Guid id) =>
        Guid.TryParse(context.Request.RouteValues[name] as string, out id);

    /// <summary>The body of the command that writes a report.</summary>
    private sealed class ReportBody
    {
        public string? Reason { get; set; }

        public string? SubjectType { get; set; }

        public Guid? SubjectId { get; set; }
    }

    /// <summary>The body of the command that opens or appeals a case.</summary>
    private sealed class DisputeBody
    {
        public string? Reason { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that asks for evidence.</summary>
    private sealed class EvidenceBody
    {
        public string? Note { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that decides a case.</summary>
    private sealed class ResolveBody
    {
        public bool AwardBase { get; set; }

        public string? Reason { get; set; }

        /// <summary>
        /// The bonus of the decision, in whole credits. It is missing when the
        /// decision carries no bonus, and it may never be negative.
        /// </summary>
        public long? Bonus { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that closes a case.</summary>
    private sealed class CloseBody
    {
        public string? Reason { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that bans an account.</summary>
    private sealed class BanBody
    {
        public string? Reason { get; set; }

        public DateTimeOffset? Until { get; set; }
    }
}
