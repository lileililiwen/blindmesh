using System.Globalization;
using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Runtime;
using Blindmesh.Application.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Tasks;

/// <summary>
/// The participation endpoints of the product. A seat belongs to a task and
/// keeps the visibility of that task: a seat of a task that the caller may not
/// see is not found and never carries the name of the project, of the owner or
/// of the contributor. Every mutating command writes its idempotency record in
/// the same transaction as the state of the seat.
/// </summary>
public static class ParticipationEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int DefaultLimit = 20;

    private const int MaxLimit = 100;

    private const int MaxAttempts = 3;

    private static readonly string[] ApplyFields = ["scope_consent", "expected_version"];

    public static void MapParticipationEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/tasks/{id}/applications", HandleApplyAsync)
            .WithName("ApplyForTaskSlot")
            .WithSummary("Apply for one slot of a published task.")
            .Accepts<ApplicationBody>("application/json")
            .Produces<ParticipationDetailView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/tasks/{id}/applications", HandleListApplicationsAsync)
            .WithName("ListTaskApplications")
            .WithSummary("List the applications of a task of the owner.")
            .Produces<PagedResult<ParticipationSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/participations/{id}", HandleGetAsync)
            .WithName("GetParticipation")
            .WithSummary("Read the committed state of one seat.")
            .Produces<ParticipationDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/v1/participations/{id}/accept", HandleAcceptAsync)
            .WithName("AcceptParticipation")
            .WithSummary("Accept an application and lease one slot of the task.")
            .Produces<ParticipationDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapPost("/v1/participations/{id}/decline", HandleDeclineAsync)
            .WithName("DeclineParticipation")
            .WithSummary("Decline an application of a task.")
            .Produces<ParticipationDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapPost("/v1/participations/{id}/start", HandleStartAsync)
            .WithName("StartParticipation")
            .WithSummary("Start the work of an accepted seat.")
            .Produces<ParticipationDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapPost("/v1/participations/{id}/withdraw", HandleWithdrawAsync)
            .WithName("WithdrawParticipation")
            .WithSummary("Withdraw a seat before the work starts.")
            .Produces<ParticipationDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);

        app.MapGet("/v1/work", HandleListWorkAsync)
            .WithName("ListWork")
            .WithSummary("List the work of the signed in contributor.")
            .Produces<PagedResult<ParticipationSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> HandleApplyAsync(
        HttpContext context,
        ParticipationService participations,
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

        if (!TryReadTaskId(context, out var taskId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<ApplicationBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            ApplyFields,
            cancellationToken);

        if (!body.IsValid)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                TaskErrorCodes.BadRequest,
                "The body of the request is not valid.");
        }

        var input = new ParticipationApplyInput(
            body.Value?.ScopeConsent ?? false,
            body.Value?.ExpectedVersion);

        return await RunCommandAsync(
            context,
            actor!,
            $"{actor!.UserId}|task.apply",
            TaskService.EscrowReference(taskId),
            key => participations.ApplyAsync(actor!, taskId, input, cancellationToken),
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleListApplicationsAsync(
        HttpContext context,
        ParticipationService participations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!TryReadTaskId(context, out var taskId))
        {
            return NotFound(context);
        }

        var query = await ReadQueryAsync(context, cancellationToken);

        if (query is null)
        {
            return BadCursor(context);
        }

        var result = await participations.ListTaskApplicationsAsync(actor!, taskId, query, cancellationToken);

        if (result.Status == ParticipationCommandStatus.NotFound)
        {
            return NotFound(context);
        }

        if (result.Status != ParticipationCommandStatus.Succeeded || result.Page is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                TaskErrorCodes.Forbidden,
                result.Message ?? "The applications of this task are not readable.");
        }

        return Results.Ok(new PagedResult<ParticipationSummaryView>
        {
            Items = result.Page.Participations,
            NextCursor = result.Page.Next?.Encode(),
        });
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        ParticipationService participations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadParticipationId(context, out var participationId))
        {
            return NotFound(context);
        }

        var result = await participations.GetAsync(actor, participationId, cancellationToken);

        if (result.Status != ParticipationCommandStatus.Succeeded || result.Participation is null)
        {
            return NotFound(context);
        }

        return Results.Ok(result.Participation);
    }

    private static async Task<IResult> HandleAcceptAsync(
        HttpContext context,
        ParticipationService participations,
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

        if (!TryReadParticipationId(context, out var participationId))
        {
            return NotFound(context);
        }

        return await RunCommandAsync(
            context,
            actor!,
            $"{actor!.UserId}|participation.accept",
            participationId.ToString("D"),
            _ => participations.AcceptAsync(actor!, participationId, cancellationToken),
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleDeclineAsync(
        HttpContext context,
        ParticipationService participations,
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

        if (!TryReadParticipationId(context, out var participationId))
        {
            return NotFound(context);
        }

        return await RunCommandAsync(
            context,
            actor!,
            $"{actor!.UserId}|participation.decline",
            participationId.ToString("D"),
            _ => participations.DeclineAsync(actor!, participationId, cancellationToken),
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleStartAsync(
        HttpContext context,
        ParticipationService participations,
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

        if (!TryReadParticipationId(context, out var participationId))
        {
            return NotFound(context);
        }

        return await RunCommandAsync(
            context,
            actor!,
            $"{actor!.UserId}|participation.start",
            participationId.ToString("D"),
            key => participations.StartAsync(actor!, participationId, key, cancellationToken),
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleWithdrawAsync(
        HttpContext context,
        ParticipationService participations,
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

        if (!TryReadParticipationId(context, out var participationId))
        {
            return NotFound(context);
        }

        return await RunCommandAsync(
            context,
            actor!,
            $"{actor!.UserId}|participation.withdraw",
            participationId.ToString("D"),
            key => participations.WithdrawAsync(actor!, participationId, key, cancellationToken),
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleListWorkAsync(
        HttpContext context,
        ParticipationService participations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadSignedInActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var query = await ReadQueryAsync(context, cancellationToken);

        if (query is null)
        {
            return BadCursor(context);
        }

        var result = await participations.ListAsync(actor!, query, cancellationToken);

        if (result.Status != ParticipationCommandStatus.Succeeded || result.Page is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                TaskErrorCodes.Forbidden,
                result.Message ?? "The work is not readable.");
        }

        return Results.Ok(new PagedResult<ParticipationSummaryView>
        {
            Items = result.Page.Participations,
            NextCursor = result.Page.Next?.Encode(),
        });
    }

    /// <summary>
    /// Runs one mutating command of a seat. The idempotency record is written
    /// in the same transaction as the state of the seat, so a replayed key
    /// never creates a second seat, a second lease or a second escrow.
    /// </summary>
    private static async Task<IResult> RunCommandAsync(
        HttpContext context,
        ParticipationActor actor,
        string idempotencyScope,
        string payload,
        Func<string?, Task<ParticipationCommandResult>> command,
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

                if (result.Status is not (ParticipationCommandStatus.Succeeded or ParticipationCommandStatus.Created))
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    return ToResult(context, result);
                }

                var responseBody = JsonSerializer.Serialize(
                    result.Participation,
                    jsonOptions.Value.SerializerOptions);

                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = result.Status == ParticipationCommandStatus.Created
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
                    statusCode: result.Status == ParticipationCommandStatus.Created
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
                        TaskErrorCodes.Conflict,
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

    private static async Task<ParticipationActor> ReadActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is null || !session.IsAuthenticated || session.IsBanned)
        {
            return ParticipationActor.Anonymous;
        }

        return new ParticipationActor(session.UserId, session.Username);
    }

    private static async Task<(ParticipationActor? Actor, IResult? Refusal)> ReadSignedInActorAsync(
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
                TaskErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                TaskErrorCodes.SessionRequired,
                "A signed in contributor is required."));
        }

        return (new ParticipationActor(session.UserId, session.Username), null);
    }

    private static async Task<(ParticipationActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
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
                TaskErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (actor, null);
    }

    private static IResult ToResult(HttpContext context, ParticipationCommandResult result) => result.Status switch
    {
        ParticipationCommandStatus.Succeeded => Results.Ok(result.Participation),
        ParticipationCommandStatus.Created => Results.Ok(result.Participation),
        ParticipationCommandStatus.NotFound => NotFound(context),
        ParticipationCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            TaskErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this contributor."),
        ParticipationCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            TaskErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        ParticipationCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.VersionConflict,
            result.Message ?? "The task was changed somewhere else."),
        ParticipationCommandStatus.InvalidTransition => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.InvalidTransition,
            result.Message ?? "The requested transition is not allowed."),
        ParticipationCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.StateConflict,
            result.Message ?? "The state of the seat does not accept this command."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The seat could not be changed."),
    };

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            TaskErrorCodes.NotFound,
            "The seat does not exist.");

    private static IResult BadCursor(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status400BadRequest,
            TaskErrorCodes.BadRequest,
            "The cursor of the list is not valid.");

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

    private static async Task<ParticipationListQuery?> ReadQueryAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var rawCursor = context.Request.Query["cursor"].ToString();
        ParticipationCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = ParticipationCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return null;
            }
        }

        return new ParticipationListQuery(ReadLimit(context.Request.Query["limit"].ToString()), cursor, null);
    }

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

    private static bool TryReadTaskId(HttpContext context, out Guid taskId) =>
        Guid.TryParse(context.Request.RouteValues["id"] as string, out taskId);

    private static bool TryReadParticipationId(HttpContext context, out Guid participationId) =>
        Guid.TryParse(context.Request.RouteValues["id"] as string, out participationId);

    /// <summary>The body of the command that applies for a seat.</summary>
    private sealed class ApplicationBody
    {
        public bool ScopeConsent { get; set; }

        public int? ExpectedVersion { get; set; }
    }
}
