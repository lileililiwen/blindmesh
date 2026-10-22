using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Operations;
using Blindmesh.Domain.Identity;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Operations;

/// <summary>
/// The operations of the product for the administrators: the backups, the
/// restore rehearsals, the deletion requests and the measured gate of the
/// pilot.
///
/// Every endpoint reads the role of the account again on every request, so a
/// demoted account loses the access at once. The public status of the product
/// is open to every visitor and it never holds a private field, an identity of
/// a person or the amount of the ledger of a person.
/// </summary>
public static class OperationsEndpoints
{
    private static readonly string[] BackupFields =
        ["kind", "location", "size_bytes", "checksum_sha256", "retention_until", "detail"];

    private static readonly string[] RehearsalFields = ["backup_id", "browser_journeys", "axe_runs"];

    private static readonly string[] DeletionFields = ["subject_user_id", "scope"];

    public static void MapOperationsEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/admin/operations/backups", HandleListBackupsAsync)
            .WithName("ListOperationBackups")
            .WithSummary("List the backups of the product, the newest first.")
            .Produces<BackupListResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapPost("/v1/admin/operations/backups", HandleRecordBackupAsync)
            .WithName("RecordOperationBackup")
            .WithSummary("Record a backup the operator took.")
            .Accepts<RecordBackupRequest>("application/json")
            .Produces<BackupView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapGet("/v1/admin/operations/restores", HandleListRehearsalsAsync)
            .WithName("ListOperationRestoreRehearsals")
            .WithSummary("List the restore rehearsals, the newest first.")
            .Produces<RestoreRehearsalListResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapPost("/v1/admin/operations/restores", HandleRunRehearsalAsync)
            .WithName("RunOperationRestoreRehearsal")
            .WithSummary("Run a restore rehearsal against an isolated database.")
            .Accepts<RunRestoreRehearsalRequest>("application/json")
            .Produces<RestoreRehearsalView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/admin/operations/deletions", HandleListDeletionsAsync)
            .WithName("ListOperationDeletionRequests")
            .WithSummary("List the deletion requests, the newest first.")
            .Produces<DeletionRequestListResponse>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapPost("/v1/admin/operations/deletions", HandleCreateDeletionAsync)
            .WithName("CreateOperationDeletionRequest")
            .WithSummary("Remove the identity of an account and keep its pseudonym.")
            .Accepts<CreateDeletionRequestRequest>("application/json")
            .Produces<DeletionRequestView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/admin/operations/gate", HandleGateAsync)
            .WithName("ReadOperationPilotGate")
            .WithSummary("Read the measured gate of the pilot.")
            .Produces<PilotGateView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden);

        app.MapGet("/v1/status", HandlePublicStatusAsync)
            .WithName("ReadPublicStatus")
            .WithSummary("Read the public status of the product.")
            .Produces<PublicStatusView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);
    }

    private static async Task<IResult> HandleListBackupsAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await operations.ListBackupsAsync(ReadActor(session!), cancellationToken);

        return Results.Ok(new BackupListResponse(result.Backups ?? []));
    }

    private static async Task<IResult> HandleRecordBackupAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return Forbidden(context, "The request does not carry the CSRF token of the session.");
        }

        var body = await ApiRequestReader.ReadAsync<RecordBackupRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            BackupFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                "The body of the request is not valid.");
        }

        var request = body.Value;

        var result = await operations.RecordBackupAsync(
            ReadActor(session!),
            new RecordBackupInput(
                request.Kind ?? "database",
                request.Location ?? string.Empty,
                request.SizeBytes ?? 0,
                request.ChecksumSha256 ?? string.Empty,
                request.RetentionUntil ?? DateTimeOffset.UtcNow.AddDays(OperationsService.RetentionDays),
                request.Detail ?? string.Empty),
            cancellationToken);

        if (result.Status == OperationsCommandStatus.ValidationFailed)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                result.Message ?? "The backup could not be recorded because some fields are not valid.",
                result.FieldErrors);
        }

        if (result.Status == OperationsCommandStatus.Forbidden)
        {
            return Forbidden(context, "This area belongs to the administrators of the product.");
        }

        return Results.Created($"/v1/admin/operations/backups/{result.Backup!.Id}", result.Backup);
    }

    private static async Task<IResult> HandleListRehearsalsAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await operations.ListRestoreRehearsalsAsync(ReadActor(session!), cancellationToken);

        return Results.Ok(new RestoreRehearsalListResponse(result.Rehearsals ?? []));
    }

    private static async Task<IResult> HandleRunRehearsalAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return Forbidden(context, "The request does not carry the CSRF token of the session.");
        }

        var body = await ApiRequestReader.ReadAsync<RunRestoreRehearsalRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            RehearsalFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                "The body of the request is not valid.");
        }

        var request = body.Value;

        var result = await operations.RunRestoreRehearsalAsync(
            ReadActor(session!),
            new RunRestoreRehearsalInput(request.BackupId, request.BrowserJourneys, request.AxeRuns),
            cancellationToken);

        if (result.Status == OperationsCommandStatus.ValidationFailed)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                result.Message ?? "The rehearsal could not start because some fields are not valid.",
                result.FieldErrors);
        }

        if (result.Status == OperationsCommandStatus.NotFound)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                OperationsErrorCodes.NotFound,
                result.Message ?? "The backup does not exist.");
        }

        if (result.Status == OperationsCommandStatus.Forbidden)
        {
            return Forbidden(context, "This area belongs to the administrators of the product.");
        }

        return Results.Ok(result.Rehearsal);
    }

    private static async Task<IResult> HandleListDeletionsAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var result = await operations.ListDeletionRequestsAsync(ReadActor(session!), cancellationToken);

        return Results.Ok(new DeletionRequestListResponse(result.Deletions ?? []));
    }

    private static async Task<IResult> HandleCreateDeletionAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return Forbidden(context, "The request does not carry the CSRF token of the session.");
        }

        var body = await ApiRequestReader.ReadAsync<CreateDeletionRequestRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            DeletionFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                "The body of the request is not valid.");
        }

        var request = body.Value;

        var result = await operations.HandleDeletionRequestAsync(
            ReadActor(session!),
            new DeletionRequestInput(request.SubjectUserId ?? Guid.Empty, request.Scope ?? string.Empty),
            cancellationToken);

        if (result.Status == OperationsCommandStatus.ValidationFailed)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                OperationsErrorCodes.BadRequest,
                result.Message ?? "The request could not be handled because some fields are not valid.",
                result.FieldErrors);
        }

        if (result.Status == OperationsCommandStatus.NotFound)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status404NotFound,
                OperationsErrorCodes.NotFound,
                result.Message ?? "The account of the request does not exist.");
        }

        if (result.Status == OperationsCommandStatus.Forbidden)
        {
            return Forbidden(context, "This area belongs to the administrators of the product.");
        }

        if (result.Status == OperationsCommandStatus.Unavailable)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status409Conflict,
                OperationsErrorCodes.Unavailable,
                result.Message ?? "The request could not be handled right now.");
        }

        return Results.Created($"/v1/admin/operations/deletions/{result.Deletion!.Id}", result.Deletion);
    }

    private static async Task<IResult> HandleGateAsync(
        HttpContext context,
        OperationsService operations,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var (session, refusal) = await ReadAdministratorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var gate = await operations.GetPilotGateAsync(ReadActor(session!), cancellationToken);

        return Results.Ok(gate);
    }

    private static async Task<IResult> HandlePublicStatusAsync(
        OperationsService operations,
        CancellationToken cancellationToken)
    {
        var status = await operations.GetPublicStatusAsync(cancellationToken);

        return Results.Ok(status);
    }

    /// <summary>
    /// Reads the actor of the request and refuses everything that is not an
    /// administrator. The role is read again on every request.
    /// </summary>
    private static async Task<(SessionResolution? Session, IResult? Refusal)> ReadAdministratorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return (
                null,
                ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    OperationsErrorCodes.SessionRequired,
                    "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (
                null,
                ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status401Unauthorized,
                    OperationsErrorCodes.SessionRequired,
                    "An administrator is required for this area."));
        }

        if (session.Role != UserRole.Administrator)
        {
            return (
                null,
                ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status403Forbidden,
                    OperationsErrorCodes.Forbidden,
                    "This area belongs to the administrators of the product."));
        }

        return (session, null);
    }

    private static OperationsActor ReadActor(SessionResolution session) =>
        new(session.UserId, session.Username, session.Role);

    private static IResult Forbidden(HttpContext context, string message) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            OperationsErrorCodes.Forbidden,
            message);
}
