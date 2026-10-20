using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Findings;
using Blindmesh.Application.Runtime;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Findings;

/// <summary>
/// The evidence endpoints of the product. A submission belongs to a seat and
/// keeps the visibility of the task of that seat: a submission that the caller
/// may not see is not found and never carries the name of the project, of the
/// owner or of the contributor. Every mutating command writes its idempotency
/// record in the same transaction as the state of the submission, and the
/// bytes of a private file never travel inside a transaction.
/// </summary>
public static class FindingsEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int MaxAttempts = 3;

    private static readonly string[] SubmissionFields =
    [
        "summary",
        "scope_completed",
        "steps",
        "tested_areas",
        "clean_result_explanation",
        "findings",
    ];

    private static readonly string[] AcceptFields = ["bonus", "expected_version"];

    private static readonly string[] RequestChangesFields = ["note", "expected_version"];

    private static readonly string[] DisputeFields = ["reason", "expected_version"];

    private static readonly string[] PatchFindingFields = ["state", "note", "expected_version"];

    private static readonly string[] AttachmentIntentFields =
    [
        "submission_id",
        "file_name",
        "content_type",
        "size_bytes",
    ];

    public static void MapFindingsEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/participations/{id}/submissions", HandleSubmitAsync)
            .WithName("SubmitParticipationEvidence")
            .WithSummary("Send the protected evidence of a seat that the caller runs.")
            .Accepts<SubmissionBody>("application/json")
            .Produces<SubmissionDetailView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/submissions/{id}", HandleGetAsync)
            .WithName("GetSubmission")
            .WithSummary("Read the evidence of a seat with its revisions and its findings.")
            .Produces<SubmissionDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapGet("/v1/projects/{id}/reviews", HandleListReviewsAsync)
            .WithName("ListProjectReviews")
            .WithSummary("List the evidence of a project that waits for the owner.")
            .Produces<IReadOnlyList<SubmissionSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPost("/v1/submissions/{id}/accept", HandleAcceptAsync)
            .WithName("AcceptSubmission")
            .WithSummary("Accept the work of a seat and settle the credits of the contributor.")
            .Accepts<AcceptBody>("application/json")
            .Produces<SubmissionDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/submissions/{id}/request-changes", HandleRequestChangesAsync)
            .WithName("RequestSubmissionChanges")
            .WithSummary("Ask the contributor for the one correction of the evidence.")
            .Accepts<RequestChangesBody>("application/json")
            .Produces<SubmissionDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/submissions/{id}/dispute", HandleDisputeAsync)
            .WithName("DisputeSubmission")
            .WithSummary("Open a dispute about the completion of a seat.")
            .Accepts<DisputeBody>("application/json")
            .Produces<SubmissionDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/findings/{id}", HandleGetFindingAsync)
            .WithName("GetFinding")
            .WithSummary("Read one finding of the evidence with its history.")
            .Produces<FindingView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/v1/findings/{id}", HandlePatchFindingAsync)
            .WithName("PatchFinding")
            .WithSummary("Classify one finding of the evidence.")
            .Accepts<PatchFindingBody>("application/json")
            .Produces<FindingView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/attachments/intents", HandleAttachmentIntentAsync)
            .WithName("CreateAttachmentIntent")
            .WithSummary("Declare a private file of the evidence.")
            .Accepts<AttachmentIntentBody>("application/json")
            .Produces<AttachmentView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/attachments/complete", HandleAttachmentCompleteAsync)
            .WithName("CompleteAttachment")
            .WithSummary("Upload the bytes of a quarantined file and release it.")
            .Produces<AttachmentView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable)
            .DisableAntiforgery();

        app.MapGet("/v1/attachments/{id}/download", HandleAttachmentDownloadAsync)
            .WithName("DownloadAttachment")
            .WithSummary("Sign the short address of a private file of the evidence.")
            .Produces<AttachmentDownloadView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status503ServiceUnavailable);
    }

    private static async Task<IResult> HandleSubmitAsync(
        HttpContext context,
        FindingsService findings,
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

        if (!TryReadRouteId(context, "id", out var participationId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<SubmissionBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            SubmissionFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new SubmissionInput(
            body.Value.Summary ?? string.Empty,
            body.Value.ScopeCompleted,
            (body.Value.Steps ?? [])
                .Select(step => new SubmissionStepInput(
                    step.Position,
                    step.Title ?? string.Empty,
                    step.Result ?? string.Empty))
                .ToArray(),
            body.Value.TestedAreas ?? [],
            body.Value.CleanResultExplanation ?? string.Empty,
            (body.Value.Findings ?? [])
                .Select(finding => new FindingInput(
                    finding.Category ?? string.Empty,
                    finding.Severity ?? string.Empty,
                    finding.Title ?? string.Empty,
                    finding.Reproduction ?? string.Empty,
                    finding.ExpectedResult ?? string.Empty,
                    finding.ActualResult ?? string.Empty))
                .ToArray());

        return await RunCommandAsync(
            context,
            $"findings:submit:{participationId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.SubmitAsync(actor!, participationId, input, cancellationToken),
            result => result.Submission,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        FindingsService findings,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadRouteId(context, "id", out var submissionId))
        {
            return NotFound(context);
        }

        var result = await findings.GetAsync(actor, submissionId, cancellationToken);

        if (result.Status != FindingsCommandStatus.Succeeded || result.Submission is null)
        {
            return NotFound(context);
        }

        return Results.Ok(result.Submission);
    }

    private static async Task<IResult> HandleListReviewsAsync(
        HttpContext context,
        FindingsService findings,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadRouteId(context, "id", out var projectId))
        {
            return NotFound(context);
        }

        var result = await findings.ListReviewsAsync(actor, projectId, cancellationToken);

        if (result.Status == FindingsCommandStatus.NotFound)
        {
            return NotFound(context);
        }

        if (result.Status != FindingsCommandStatus.Succeeded || result.Reviews is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                FindingsErrorCodes.Forbidden,
                result.Message ?? "The reviews of this project are not readable.");
        }

        return Results.Ok(result.Reviews);
    }

    private static async Task<IResult> HandleAcceptAsync(
        HttpContext context,
        FindingsService findings,
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

        var body = await ApiRequestReader.ReadAsync<AcceptBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            AcceptFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new AcceptSubmissionInput(body.Value.Bonus, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"findings:accept:{submissionId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.AcceptAsync(actor!, submissionId, input, key, cancellationToken),
            result => result.Submission,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleRequestChangesAsync(
        HttpContext context,
        FindingsService findings,
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

        var body = await ApiRequestReader.ReadAsync<RequestChangesBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            RequestChangesFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new RequestChangesInput(body.Value.Note ?? string.Empty, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"findings:request-changes:{submissionId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.RequestChangesAsync(actor!, submissionId, input, cancellationToken),
            result => result.Submission,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleDisputeAsync(
        HttpContext context,
        FindingsService findings,
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

        var input = new DisputeSubmissionInput(body.Value.Reason ?? string.Empty, body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"findings:dispute:{submissionId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.DisputeAsync(actor!, submissionId, input, cancellationToken),
            result => result.Submission,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleGetFindingAsync(
        HttpContext context,
        FindingsService findings,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadRouteId(context, "id", out var findingId))
        {
            return NotFound(context);
        }

        var result = await findings.GetFindingAsync(actor, findingId, cancellationToken);

        if (result.Status != FindingsCommandStatus.Succeeded || result.Finding is null)
        {
            return NotFound(context);
        }

        return Results.Ok(result.Finding);
    }

    private static async Task<IResult> HandlePatchFindingAsync(
        HttpContext context,
        FindingsService findings,
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

        if (!TryReadRouteId(context, "id", out var findingId))
        {
            return NotFound(context);
        }

        var body = await ApiRequestReader.ReadAsync<PatchFindingBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            PatchFindingFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new PatchFindingInput(
            body.Value.State ?? string.Empty,
            body.Value.Note,
            body.Value.ExpectedVersion);

        return await RunCommandAsync(
            context,
            $"findings:patch-finding:{findingId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.PatchFindingAsync(actor!, findingId, input, cancellationToken),
            result => result.Finding,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleAttachmentIntentAsync(
        HttpContext context,
        FindingsService findings,
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

        var body = await ApiRequestReader.ReadAsync<AttachmentIntentBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            AttachmentIntentFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return BadRequest(context);
        }

        var input = new AttachmentIntentInput(
            body.Value.SubmissionId,
            body.Value.FileName ?? string.Empty,
            body.Value.ContentType ?? string.Empty,
            body.Value.SizeBytes);

        return await RunCommandAsync(
            context,
            $"findings:attachment-intent:{body.Value.SubmissionId:D}",
            PayloadHasher.Compute(body.Raw),
            key => findings.CreateAttachmentIntentAsync(actor!, input, cancellationToken),
            result => result.Attachment,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    /// <summary>
    /// The upload of the bytes of a quarantined file. The bytes are read and
    /// checked before the transaction of the command opens, so the network of
    /// the private object storage never runs inside a transaction.
    /// </summary>
    private static async Task<IResult> HandleAttachmentCompleteAsync(
        HttpContext context,
        FindingsService findings,
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

        if (!context.Request.HasFormContentType)
        {
            return BadRequest(context);
        }

        IFormCollection form;

        try
        {
            form = await context.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return TooLarge(context);
        }

        var rawId = form["attachment_id"].ToString();
        var file = form.Files["file"];

        if (!Guid.TryParse(rawId, out var attachmentId) || file is null || file.Length == 0)
        {
            return BadRequest(context);
        }

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

        var payloadHash = PayloadHasher.Compute($"{attachmentId:D}|{file.Length}");
        var existing = await idempotencyStore.FindAsync(
            $"findings:attachment-complete:{attachmentId:D}",
            key,
            cancellationToken);

        if (existing is not null)
        {
            return ReplayOrReject(context, existing, payloadHash);
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return TooLarge(context);
        }

        byte[] content;

        using (var buffer = new MemoryStream())
        {
            await using var stream = file.OpenReadStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            content = buffer.ToArray();
        }

        AttachmentPreparation preparation;

        try
        {
            preparation = await findings.PrepareAttachmentAsync(
                actor!,
                attachmentId,
                content,
                cancellationToken);
        }
        catch (ObjectStorageUnavailableException)
        {
            return Unavailable(context);
        }

        // The release writes the row of the file: a file that passed the check
        // becomes readable and a file that failed the check stays refused.
        return await RunCommandAsync(
            context,
            $"findings:attachment-complete:{attachmentId:D}",
            payloadHash,
            _ => findings.ReleaseAttachmentAsync(actor!, attachmentId, preparation, cancellationToken),
            result => result.Attachment,
            idempotencyStore,
            transactionFactory,
            scopeFactory,
            jsonOptions,
            clock,
            cancellationToken);
    }

    private static async Task<IResult> HandleAttachmentDownloadAsync(
        HttpContext context,
        FindingsService findings,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadRouteId(context, "id", out var attachmentId))
        {
            return NotFound(context);
        }

        FindingsCommandResult result;

        try
        {
            result = await findings.CreateDownloadAsync(actor, attachmentId, cancellationToken);
        }
        catch (ObjectStorageUnavailableException)
        {
            return Unavailable(context);
        }

        if (result.Status == FindingsCommandStatus.NotFound)
        {
            return NotFound(context);
        }

        if (result.Status == FindingsCommandStatus.StateConflict)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status409Conflict,
                FindingsErrorCodes.StateConflict,
                result.Message ?? "The file is not released yet.");
        }

        if (result.Status != FindingsCommandStatus.Succeeded || result.Download is null)
        {
            return NotFound(context);
        }

        return Results.Ok(result.Download);
    }

    /// <summary>
    /// Runs one mutating command of the findings module. The idempotency
    /// record is written in the same transaction as the state of the command,
    /// so a replayed key never creates a second submission and never moves a
    /// credit twice.
    /// </summary>
    private static async Task<IResult> RunCommandAsync<T>(
        HttpContext context,
        string idempotencyScope,
        string payloadHash,
        Func<string?, Task<FindingsCommandResult>> command,
        Func<FindingsCommandResult, T?> body,
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

                if (result.Status is not (FindingsCommandStatus.Succeeded or FindingsCommandStatus.Created))
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
                        StatusCode = result.Status == FindingsCommandStatus.Created
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
                    statusCode: result.Status == FindingsCommandStatus.Created
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
                        FindingsErrorCodes.Conflict,
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

    private static async Task<FindingsActor> ReadActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is null || !session.IsAuthenticated || session.IsBanned)
        {
            return FindingsActor.Anonymous;
        }

        return new FindingsActor(session.UserId, session.Username);
    }

    private static async Task<(FindingsActor? Actor, IResult? Refusal)> ReadSignedInActorAsync(
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
                FindingsErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                FindingsErrorCodes.SessionRequired,
                "A signed in contributor is required."));
        }

        return (new FindingsActor(session.UserId, session.Username), null);
    }

    private static async Task<(FindingsActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
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
                FindingsErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (actor, null);
    }

    private static IResult ToResult(HttpContext context, FindingsCommandResult result) => result.Status switch
    {
        FindingsCommandStatus.Succeeded => Results.Ok(result.Submission),
        FindingsCommandStatus.Created => Results.Ok(result.Submission),
        FindingsCommandStatus.NotFound => NotFound(context),
        FindingsCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            FindingsErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this contributor."),
        FindingsCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            FindingsErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        FindingsCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            FindingsErrorCodes.VersionConflict,
            result.Message ?? "The evidence was changed somewhere else."),
        FindingsCommandStatus.InvalidTransition => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            FindingsErrorCodes.InvalidTransition,
            result.Message ?? "The requested transition is not allowed."),
        FindingsCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            FindingsErrorCodes.StateConflict,
            result.Message ?? "The state of the evidence does not accept this command."),
        FindingsCommandStatus.PayloadTooLarge => TooLarge(context),
        FindingsCommandStatus.Unavailable => Unavailable(context),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The evidence could not be changed."),
    };

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            FindingsErrorCodes.NotFound,
            "The resource does not exist.");

    private static IResult BadRequest(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status400BadRequest,
            FindingsErrorCodes.BadRequest,
            "The request is not valid.");

    private static IResult TooLarge(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status413PayloadTooLarge,
            FindingsErrorCodes.PayloadTooLarge,
            "The file is larger than the allowed size.");

    private static IResult Unavailable(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status503ServiceUnavailable,
            FindingsErrorCodes.Unavailable,
            "The private object storage is not reachable.");

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

    /// <summary>The body of the command that sends the evidence of a seat.</summary>
    private sealed class SubmissionBody
    {
        public string? Summary { get; set; }

        public bool ScopeCompleted { get; set; }

        public List<StepBody>? Steps { get; set; }

        public List<string>? TestedAreas { get; set; }

        public string? CleanResultExplanation { get; set; }

        public List<FindingBody>? Findings { get; set; }
    }

    private sealed class StepBody
    {
        public int Position { get; set; }

        public string? Title { get; set; }

        public string? Result { get; set; }
    }

    private sealed class FindingBody
    {
        public string? Category { get; set; }

        public string? Severity { get; set; }

        public string? Title { get; set; }

        public string? Reproduction { get; set; }

        public string? ExpectedResult { get; set; }

        public string? ActualResult { get; set; }
    }

    /// <summary>The body of the command that accepts the work of a seat.</summary>
    private sealed class AcceptBody
    {
        public long Bonus { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that asks for the one correction.</summary>
    private sealed class RequestChangesBody
    {
        public string? Note { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that opens a dispute.</summary>
    private sealed class DisputeBody
    {
        public string? Reason { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that classifies a finding.</summary>
    private sealed class PatchFindingBody
    {
        public string? State { get; set; }

        public string? Note { get; set; }

        public int? ExpectedVersion { get; set; }
    }

    /// <summary>The body of the command that declares a private file.</summary>
    private sealed class AttachmentIntentBody
    {
        public Guid SubmissionId { get; set; }

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public long SizeBytes { get; set; }
    }
}
