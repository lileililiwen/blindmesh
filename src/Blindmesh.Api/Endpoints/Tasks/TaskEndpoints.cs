using System.Globalization;
using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Runtime;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Tasks;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Tasks;

/// <summary>
/// The task endpoints of the product. A task belongs to a project and keeps
/// the visibility of that project: a task of a private project is not found
/// and never carries the name of the project or of the contributor. The
/// publication reserves the maximum budget of the escrow in the same
/// transaction as the state of the task.
/// </summary>
public static class TaskEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int DefaultLimit = 20;

    private const int MaxLimit = 100;

    private const int MaxAttempts = 3;

    private static readonly string[] CreateFields =
    [
        "project_id",
        "title",
        "scope",
        "modes",
        "visibility",
        "expected_minutes",
        "base_reward",
        "slot_bonus",
        "slot_count",
        "deadline",
        "accepts_clean_findings",
        "access_kind",
        "access_resource",
        "review_criteria",
        "requirements",
        "steps",
    ];

    private static readonly string[] UpdateFields =
    [
        .. CreateFields,
        "expected_version",
    ];

    public static void MapTaskEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/tasks", HandleListAsync)
            .WithName("ListTasks")
            .WithSummary("List the tasks of the marketplace that the caller may see.")
            .Produces<PagedResult<TaskSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);

        app.MapPost("/v1/tasks", HandleCreateAsync)
            .WithName("CreateTask")
            .WithSummary("Write a task in the draft state.")
            .Accepts<TaskBody>("application/json")
            .Produces<TaskDetailView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/tasks/{id}", HandleGetAsync)
            .WithName("GetTask")
            .WithSummary("Read the committed state of a task.")
            .Produces<TaskDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/v1/tasks/{id}", HandleUpdateAsync)
            .WithName("UpdateTask")
            .WithSummary("Change the editable content of a task.")
            .Accepts<TaskBody>("application/json")
            .Produces<TaskDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/tasks/{id}/publish", HandlePublishAsync)
            .WithName("PublishTask")
            .WithSummary("Publish a task and reserve its maximum budget.")
            .Produces<TaskDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/tasks/{id}/cancel", HandleCancelAsync)
            .WithName("CancelTask")
            .WithSummary("Close the admission of a task and refund its unused slots.")
            .Produces<TaskDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext context,
        TaskService taskService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        var rawCursor = context.Request.Query["cursor"].ToString();
        TaskCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = TaskCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    TaskErrorCodes.BadRequest,
                    "The cursor of the list is not valid.");
            }
        }

        var mode = ReadMode(context.Request.Query["mode"].ToString());

        if (mode is null && !string.IsNullOrWhiteSpace(context.Request.Query["mode"].ToString()))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                TaskErrorCodes.BadRequest,
                "The mode of the list is not valid. Use explore, verify, access or review.");
        }

        var requirement = ReadRequirement(context.Request.Query["requirement"].ToString());

        if (requirement is null && !string.IsNullOrWhiteSpace(context.Request.Query["requirement"].ToString()))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                TaskErrorCodes.BadRequest,
                "The requirement of the list is not valid. Use kind:value with kind region, device or role.");
        }

        Guid? projectId = null;
        var rawProjectId = context.Request.Query["project_id"].ToString();

        if (!string.IsNullOrWhiteSpace(rawProjectId))
        {
            if (!Guid.TryParse(rawProjectId, out var parsedProjectId))
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    TaskErrorCodes.BadRequest,
                    "The project of the list is not valid.");
            }

            projectId = parsedProjectId;
        }

        var query = new TaskListQuery(
            ReadLimit(context.Request.Query["limit"].ToString()),
            cursor,
            mode,
            requirement?.Kind,
            requirement?.Value,
            projectId);

        var page = await taskService.ListAsync(actor, query, cancellationToken);

        return Results.Ok(page);
    }

    private static async Task<IResult> HandleCreateAsync(
        HttpContext context,
        TaskService taskService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ApiRequestReader.ReadAsync<TaskBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            CreateFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                TaskErrorCodes.BadRequest,
                "The request body is not valid. It holds only the fields of a task.");
        }

        if (body.Value.ProjectId is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                TaskErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                new Dictionary<string, string[]>
                {
                    ["project_id"] = ["A task belongs to a project."],
                });
        }

        var (input, errors) = ToInput(body.Value);

        if (errors.Count > 0)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                TaskErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                errors);
        }

        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var result = await taskService.CreateAsync(actor!, input!, cancellationToken);

        if (result.Status != TaskCommandStatus.Created)
        {
            return ToResult(context, result);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Created($"{context.Request.Path}", result.Task);
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        TaskService taskService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        if (!TryReadTaskId(context, out var taskId))
        {
            return NotFound(context);
        }

        var result = await taskService.GetAsync(actor, taskId, cancellationToken);

        return ToResult(context, result);
    }

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext context,
        TaskService taskService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
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

        var body = await ApiRequestReader.ReadAsync<TaskBody>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            UpdateFields,
            cancellationToken);

        if (!body.IsValid || body.Value is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                TaskErrorCodes.BadRequest,
                "The request body is not valid. It holds only the fields of a task.");
        }

        if (body.Value.ExpectedVersion is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                TaskErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                new Dictionary<string, string[]>
                {
                    ["expected_version"] = ["The expected version of the task is required."],
                });
        }

        var (input, errors) = ToInput(body.Value);

        if (errors.Count > 0)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                TaskErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                errors);
        }

        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var result = await taskService.UpdateAsync(
            actor!,
            taskId,
            input!,
            body.Value.ExpectedVersion.Value,
            cancellationToken);

        if (result.Status != TaskCommandStatus.Succeeded)
        {
            return ToResult(context, result);
        }

        await transaction.CommitAsync(cancellationToken);

        return Results.Ok(result.Task);
    }

    private static async Task<IResult> HandlePublishAsync(
        HttpContext context,
        TaskService taskService,
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

        var payloadHash = PayloadHasher.Compute(taskId.ToString("D"));
        var idempotencyScope = $"{actor!.UserId}|task.publish";
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

                var result = await taskService.PublishAsync(actor!, taskId, key, cancellationToken);

                if (result.Status != TaskCommandStatus.Succeeded)
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    return ToResult(context, result);
                }

                var responseBody = JsonSerializer.Serialize(result.Task, jsonOptions.Value.SerializerOptions);

                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = StatusCodes.Status200OK,
                        ResponseBody = responseBody,
                        CreatedAt = clock.UtcNow,
                        ExpiresAt = clock.UtcNow.AddDays(7),
                    },
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status200OK);
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

    private static async Task<IResult> HandleCancelAsync(
        HttpContext context,
        TaskService taskService,
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

        var payloadHash = PayloadHasher.Compute(taskId.ToString("D"));
        var idempotencyScope = $"{actor!.UserId}|task.cancel";
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

                var result = await taskService.CancelAsync(actor!, taskId, key, cancellationToken);

                if (result.Status != TaskCommandStatus.Succeeded)
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                        transaction = null;
                    }

                    return ToResult(context, result);
                }

                var responseBody = JsonSerializer.Serialize(result.Task, jsonOptions.Value.SerializerOptions);

                await idempotencyStore.SaveAsync(
                    new IdempotencyRecord
                    {
                        Scope = idempotencyScope,
                        Key = key,
                        PayloadHash = payloadHash,
                        StatusCode = StatusCodes.Status200OK,
                        ResponseBody = responseBody,
                        CreatedAt = clock.UtcNow,
                        ExpiresAt = clock.UtcNow.AddDays(7),
                    },
                    cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status200OK);
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

    private static async Task<TaskActor> ReadActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is null || !session.IsAuthenticated || session.IsBanned)
        {
            return TaskActor.Anonymous;
        }

        return new TaskActor(session.UserId, session.Username);
    }

    private static async Task<(TaskActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
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
                "A signed in contributor is required to change a task."));
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                TaskErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (new TaskActor(session.UserId, session.Username), null);
    }

    private static IResult ToResult(HttpContext context, TaskCommandResult result) => result.Status switch
    {
        TaskCommandStatus.Succeeded => Results.Ok(result.Task),
        TaskCommandStatus.Created => Results.Ok(result.Task),
        TaskCommandStatus.NotFound => NotFound(context),
        TaskCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            TaskErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this contributor."),
        TaskCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            TaskErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        TaskCommandStatus.InsufficientFunds => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            TaskErrorCodes.InsufficientFunds,
            result.Message ?? "The account does not hold the budget of the task.",
            result.FieldErrors),
        TaskCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.VersionConflict,
            result.Message ?? "The task was changed somewhere else."),
        TaskCommandStatus.InvalidTransition => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.InvalidTransition,
            result.Message ?? "The requested transition is not allowed."),
        TaskCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            TaskErrorCodes.StateConflict,
            result.Message ?? "The state of the task does not accept this command."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The task could not be changed."),
    };

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            TaskErrorCodes.NotFound,
            "The task does not exist.");

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

    private static bool TryReadTaskId(HttpContext context, out Guid taskId) =>
        Guid.TryParse(context.Request.RouteValues["id"] as string, out taskId);

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

    /// <summary>
    /// Reads the modes of the list. The caller names one mode or several, and
    /// a task has to carry all of them.
    /// </summary>
    private static TaskMode? ReadMode(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var modes = TaskMode.None;

        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var mode = part.ToLowerInvariant() switch
            {
                "explore" => TaskMode.Explore,
                "verify" => TaskMode.Verify,
                "access" => TaskMode.Access,
                "review" => TaskMode.Review,
                _ => TaskMode.None,
            };

            if (mode == TaskMode.None)
            {
                return null;
            }

            modes |= mode;
        }

        return modes == TaskMode.None ? null : modes;
    }

    /// <summary>
    /// Reads the hard requirement of the list. The caller names the kind and
    /// the value of the requirement, or only the value.
    /// </summary>
    private static (string? Kind, string? Value)? ReadRequirement(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var separator = raw.IndexOf(':', StringComparison.Ordinal);

        if (separator < 0)
        {
            return (null, raw.Trim());
        }

        var kind = raw[..separator].Trim().ToLowerInvariant();
        var value = raw[(separator + 1)..].Trim();

        if (!TaskRules.RequirementKinds.Contains(kind, StringComparer.Ordinal))
        {
            return null;
        }

        if (value.Length == 0)
        {
            return null;
        }

        return (kind, value);
    }

    /// <summary>
    /// Reads the body of a task into the input of the service. The body holds
    /// no credential and no secret.
    /// </summary>
    private static (TaskInput? Input, Dictionary<string, string[]> Errors) ToInput(TaskBody body)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        void Add(string field, string message)
        {
            errors[field] = [message];
        }

        var modes = TaskMode.None;

        foreach (var mode in body.Modes ?? [])
        {
            var parsed = mode.Trim().ToLowerInvariant() switch
            {
                "explore" => TaskMode.Explore,
                "verify" => TaskMode.Verify,
                "access" => TaskMode.Access,
                "review" => TaskMode.Review,
                _ => TaskMode.None,
            };

            if (parsed == TaskMode.None)
            {
                Add("modes", "A mode is explore, verify, access or review.");
                break;
            }

            modes |= parsed;
        }

        TaskVisibility? visibility = null;

        if (!string.IsNullOrWhiteSpace(body.Visibility))
        {
            visibility = body.Visibility.Trim().ToLowerInvariant() switch
            {
                "public" => TaskVisibility.Public,
                "matched" => TaskVisibility.Matched,
                "private" => TaskVisibility.Private,
                _ => null,
            };

            if (visibility is null)
            {
                Add("visibility", "The visibility is public, matched or private.");
            }
        }

        TaskAccessKind? accessKind = null;

        if (!string.IsNullOrWhiteSpace(body.AccessKind))
        {
            accessKind = body.AccessKind.Trim().ToLowerInvariant() switch
            {
                "owner_operated" => TaskAccessKind.OwnerOperated,
                "delegated_consent" => TaskAccessKind.DelegatedConsent,
                _ => null,
            };

            if (accessKind is null)
            {
                Add("access_kind", "The access is owner_operated or delegated_consent.");
            }
        }

        var requirements = (body.Requirements ?? [])
            .Select(requirement => new TaskRequirementInput(
                requirement.Kind?.Trim() ?? string.Empty,
                requirement.Value?.Trim() ?? string.Empty))
            .ToArray();

        var steps = (body.Steps ?? [])
            .Select(step => new TaskStepInput(
                step.Title?.Trim() ?? string.Empty,
                step.ExpectedResult?.Trim() ?? string.Empty))
            .ToArray();

        var input = new TaskInput
        {
            ProjectId = body.ProjectId ?? Guid.Empty,
            Title = body.Title?.Trim() ?? string.Empty,
            Scope = body.Scope?.Trim() ?? string.Empty,
            Modes = modes,
            Visibility = visibility ?? TaskVisibility.Private,
            ExpectedMinutes = body.ExpectedMinutes ?? 0,
            BaseReward = body.BaseReward ?? 0,
            SlotBonus = body.SlotBonus ?? 0,
            SlotCount = body.SlotCount ?? 0,
            Deadline = body.Deadline,
            AcceptsCleanFindings = body.AcceptsCleanFindings ?? false,
            AccessKind = accessKind,
            AccessResource = body.AccessResource?.Trim(),
            ReviewCriteria = body.ReviewCriteria?.Trim(),
            Requirements = requirements,
            Steps = steps,
        };

        return (input, errors);
    }
}

/// <summary>The body of a task command. It holds no credential.</summary>
public sealed record TaskBody(
    Guid? ProjectId,
    string? Title,
    string? Scope,
    IReadOnlyList<string>? Modes,
    string? Visibility,
    int? ExpectedMinutes,
    long? BaseReward,
    long? SlotBonus,
    int? SlotCount,
    DateTimeOffset? Deadline,
    bool? AcceptsCleanFindings,
    string? AccessKind,
    string? AccessResource,
    string? ReviewCriteria,
    IReadOnlyList<TaskRequirementBody>? Requirements,
    IReadOnlyList<TaskStepBody>? Steps,
    int? ExpectedVersion);

/// <summary>One hard requirement of a task.</summary>
public sealed record TaskRequirementBody(string? Kind, string? Value);

/// <summary>One step of the verification of a task.</summary>
public sealed record TaskStepBody(string? Title, string? ExpectedResult);
