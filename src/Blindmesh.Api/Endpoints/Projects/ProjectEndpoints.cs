using System.Globalization;
using System.Text.Json;
using Blindmesh.Api.Endpoints.Identity;
using Blindmesh.Api.Infrastructure;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Projects;
using Blindmesh.Application.Runtime;
using Blindmesh.Domain.Projects;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Endpoints.Projects;

/// <summary>
/// The project endpoints of the product. The visibility of a project is
/// enforced on every read, list, detail and membership operation, and the
/// actor policy is checked again on every command.
/// </summary>
public static class ProjectEndpoints
{
    public const string IdempotencyHeaderName = "Idempotency-Key";

    private const int DefaultLimit = 20;

    private const int MaxLimit = 100;

    public static void MapProjectEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/projects", HandleListAsync)
            .WithName("ListProjects")
            .WithSummary("List the projects the caller may see.")
            .Produces<PagedResult<ProjectSummaryView>>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest);

        app.MapPost("/v1/projects", HandleCreateAsync)
            .WithName("CreateProject")
            .WithSummary("Create a project in the draft state.")
            .Accepts<CreateProjectRequest>("application/json")
            .Produces<ProjectDetailView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapGet("/v1/projects/{id}", HandleGetAsync)
            .WithName("ReadProject")
            .WithSummary("Read the committed state of a project.")
            .Produces<ProjectDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound);

        app.MapPatch("/v1/projects/{id}", HandleUpdateAsync)
            .WithName("UpdateProject")
            .WithSummary("Save the content or the visibility of a project.")
            .Accepts<UpdateProjectRequest>("application/json")
            .Produces<ProjectDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/projects/{id}/transitions", HandleTransitionAsync)
            .WithName("TransitionProject")
            .WithSummary("Apply a transition of the lifecycle of a project.")
            .Accepts<TransitionProjectRequest>("application/json")
            .Produces<ProjectDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPut("/v1/projects/{id}/members/{user_id}", HandleAddMemberAsync)
            .WithName("AddProjectMember")
            .WithSummary("Add a member to a project or change the role of a member.")
            .Accepts<AddProjectMemberRequest>("application/json")
            .Produces<ProjectDetailView>(StatusCodes.Status200OK)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapDelete("/v1/projects/{id}/members/{user_id}", HandleRemoveMemberAsync)
            .WithName("RemoveProjectMember")
            .WithSummary("Remove a member of a project.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);

        app.MapPost("/v1/projects/{id}/versions", HandleAddVersionAsync)
            .WithName("AddProjectVersion")
            .WithSummary("Record a released version of a project.")
            .Accepts<AddProjectVersionRequest>("application/json")
            .Produces<ProjectDetailView>(StatusCodes.Status201Created)
            .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ApiErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ApiErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ApiErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ApiErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity);
    }

    private static async Task<IResult> HandleListAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var actor = await ReadActorAsync(context, guard, cancellationToken);

        var rawCursor = context.Request.Query["cursor"].ToString();
        ProjectCursor? cursor = null;

        if (!string.IsNullOrWhiteSpace(rawCursor))
        {
            cursor = ProjectCursor.Decode(rawCursor);

            if (cursor is null)
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status400BadRequest,
                    ProjectErrorCodes.BadRequest,
                    "The cursor of the list is not valid.");
            }
        }

        var limit = ReadLimit(context.Request.Query["limit"].ToString());
        var page = await projectService.ListAsync(actor, limit, cursor, cancellationToken);

        return Results.Ok(page);
    }

    private static async Task<IResult> HandleGetAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var actor = await ReadActorAsync(context, guard, cancellationToken);
        var result = await projectService.GetAsync(actor, projectId, cancellationToken);

        return ToResult(context, result);
    }

    private static async Task<IResult> HandleCreateAsync(
        HttpContext context,
        ProjectService projectService,
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

        var body = await ProjectRequestReader.ReadAsync<CreateProjectRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        var request = body.Value;

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ProjectErrorCodes.BadRequest,
                "The request body is not valid JSON.");
        }

        if (!TryParseVisibility(request.Visibility, out var visibility))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ProjectErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                new Dictionary<string, string[]>
                {
                    ["visibility"] = ["The visibility must be public, private or matched."],
                });
        }

        var input = new ProjectInput
        {
            Name = request.Name ?? string.Empty,
            Summary = request.Summary ?? string.Empty,
            Description = request.Description ?? string.Empty,
            RepositoryUrl = request.RepositoryUrl ?? string.Empty,
            Visibility = visibility,
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
        var idempotencyScope = $"{actor!.UserId}|project.create";

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
            var result = await projectService.CreateAsync(actor!, input, cancellationToken);

            if (result.Status != ProjectCommandStatus.Created)
            {
                return ToResult(context, result);
            }

            var responseBody = JsonSerializer.Serialize(result.Project, jsonOptions.Value.SerializerOptions);

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

    private static async Task<IResult> HandleUpdateAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ProjectRequestReader.ReadAsync<UpdateProjectRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        var request = body.Value;

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ProjectErrorCodes.BadRequest,
                "The request body is not valid JSON.");
        }

        if (request.ExpectedVersion is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ProjectErrorCodes.Unprocessable,
                "The expected version of the project is required.",
                new Dictionary<string, string[]>
                {
                    ["expected_version"] = ["The version of the project that was read is required."],
                });
        }

        ProjectVisibility? visibility = null;

        if (request.Visibility is not null)
        {
            if (!TryParseVisibility(request.Visibility, out var parsed))
            {
                return ApiErrorWriter.Create(
                    context,
                    StatusCodes.Status422UnprocessableEntity,
                    ProjectErrorCodes.Unprocessable,
                    "Some fields of the request are not valid.",
                    new Dictionary<string, string[]>
                    {
                        ["visibility"] = ["The visibility must be public, private or matched."],
                    });
            }

            visibility = parsed;
        }

        var patch = new ProjectPatch
        {
            Name = request.Name,
            Summary = request.Summary,
            Description = request.Description,
            RepositoryUrl = request.RepositoryUrl,
            Visibility = visibility,
        };

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => projectService.UpdateAsync(actor!, projectId, patch, request.ExpectedVersion!.Value, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> HandleTransitionAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ProjectRequestReader.ReadAsync<TransitionProjectRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        var request = body.Value;

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ProjectErrorCodes.BadRequest,
                "The request body is not valid JSON.");
        }

        if (request.ExpectedVersion is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ProjectErrorCodes.Unprocessable,
                "The expected version of the project is required.",
                new Dictionary<string, string[]>
                {
                    ["expected_version"] = ["The version of the project that was read is required."],
                });
        }

        if (!TryParseStatus(request.To, out var target))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ProjectErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                new Dictionary<string, string[]>
                {
                    ["to"] = ["The target state must be active, paused or archived."],
                });
        }

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => projectService.TransitionAsync(actor!, projectId, target, request.ExpectedVersion!.Value, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> HandleAddMemberAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ProjectRequestReader.ReadAsync<AddProjectMemberRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        var request = body.Value;

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ProjectErrorCodes.BadRequest,
                "The request body is not valid JSON.");
        }

        if (!TryParseMemberRole(request.Role, out var role))
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status422UnprocessableEntity,
                ProjectErrorCodes.Unprocessable,
                "Some fields of the request are not valid.",
                new Dictionary<string, string[]>
                {
                    ["role"] = ["The role must be viewer or editor."],
                });
        }

        var userReference = ReadUserReference(context);

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => projectService.AddMemberAsync(actor!, projectId, userReference, role, cancellationToken),
            cancellationToken);
    }

    private static async Task<IResult> HandleRemoveMemberAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var userReference = ReadUserReference(context);

        return await RunCommandAsync(
            context,
            transactionFactory,
            () => projectService.RemoveMemberAsync(actor!, projectId, userReference, cancellationToken),
            cancellationToken,
            noContent: true);
    }

    private static async Task<IResult> HandleAddVersionAsync(
        HttpContext context,
        ProjectService projectService,
        IdentitySessionGuard guard,
        ICommandTransactionFactory transactionFactory,
        IOptions<JsonOptions> jsonOptions,
        CancellationToken cancellationToken)
    {
        if (!TryReadProjectId(context, out var projectId))
        {
            return NotFound(context);
        }

        var (actor, refusal) = await ReadWriteActorAsync(context, guard, cancellationToken);

        if (refusal is not null)
        {
            return refusal;
        }

        var body = await ProjectRequestReader.ReadAsync<AddProjectVersionRequest>(
            context.Request,
            jsonOptions.Value.SerializerOptions,
            cancellationToken);

        var request = body.Value;

        if (request is null)
        {
            return ApiErrorWriter.Create(
                context,
                StatusCodes.Status400BadRequest,
                ProjectErrorCodes.BadRequest,
                "The request body is not valid JSON.");
        }

        return await RunCommandAsync(
            context,
            transactionFactory,
            async () =>
            {
                var result = await projectService.AddVersionAsync(
                    actor!,
                    projectId,
                    request.Label,
                    request.CommitReference,
                    cancellationToken);

                return result.Status == ProjectCommandStatus.Succeeded
                    ? result with { Status = ProjectCommandStatus.Created }
                    : result;
            },
            cancellationToken,
            created: true);
    }

    /// <summary>
    /// Runs a command inside the transaction of the request. A refused command
    /// never commits, so the data of the project stays as it was.
    /// </summary>
    private static async Task<IResult> RunCommandAsync(
        HttpContext context,
        ICommandTransactionFactory transactionFactory,
        Func<Task<ProjectCommandResult>> command,
        CancellationToken cancellationToken,
        bool created = false,
        bool noContent = false)
    {
        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var result = await command();

        if (result.Status is not (ProjectCommandStatus.Succeeded or ProjectCommandStatus.Created))
        {
            return ToResult(context, result);
        }

        await transaction.CommitAsync(cancellationToken);

        if (noContent)
        {
            return Results.NoContent();
        }

        return created
            ? Results.Created($"{context.Request.Path}", result.Project)
            : Results.Ok(result.Project);
    }

    private static async Task<ProjectActor> ReadActorAsync(
        HttpContext context,
        IdentitySessionGuard guard,
        CancellationToken cancellationToken)
    {
        var session = await guard.ResolveAsync(context, cancellationToken);

        if (session is { IsBanned: true })
        {
            return ProjectActor.Anonymous;
        }

        if (session is null || !session.IsAuthenticated)
        {
            return ProjectActor.Anonymous;
        }

        return new ProjectActor(session.UserId, session.Username);
    }

    private static async Task<(ProjectActor? Actor, IResult? Refusal)> ReadWriteActorAsync(
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
                ProjectErrorCodes.SessionInvalid,
                "The session of this account is no longer valid."));
        }

        if (session is null || !session.IsAuthenticated)
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status401Unauthorized,
                ProjectErrorCodes.SessionRequired,
                "A signed in contributor is required to change a project."));
        }

        if (!guard.HasValidCsrfToken(context))
        {
            return (null, ApiErrorWriter.Create(
                context,
                StatusCodes.Status403Forbidden,
                ProjectErrorCodes.CsrfTokenInvalid,
                "The request does not carry the CSRF token of the session."));
        }

        return (new ProjectActor(session.UserId, session.Username), null);
    }

    private static IResult ToResult(HttpContext context, ProjectCommandResult result) => result.Status switch
    {
        ProjectCommandStatus.Succeeded => Results.Ok(result.Project),
        ProjectCommandStatus.Created => Results.Ok(result.Project),
        ProjectCommandStatus.NotFound => NotFound(context),
        ProjectCommandStatus.Forbidden => ApiErrorWriter.Create(
            context,
            StatusCodes.Status403Forbidden,
            ProjectErrorCodes.Forbidden,
            result.Message ?? "The command is not allowed for this contributor."),
        ProjectCommandStatus.ValidationFailed => ApiErrorWriter.Create(
            context,
            StatusCodes.Status422UnprocessableEntity,
            ProjectErrorCodes.Unprocessable,
            "Some fields of the request are not valid.",
            result.FieldErrors),
        ProjectCommandStatus.VersionConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ProjectErrorCodes.VersionConflict,
            result.Message ?? "The project was changed somewhere else."),
        ProjectCommandStatus.InvalidTransition => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ProjectErrorCodes.InvalidTransition,
            result.Message ?? "The requested transition is not allowed."),
        ProjectCommandStatus.StateConflict => ApiErrorWriter.Create(
            context,
            StatusCodes.Status409Conflict,
            ProjectErrorCodes.StateConflict,
            result.Message ?? "The state of the project does not accept this command."),
        _ => ApiErrorWriter.Create(
            context,
            StatusCodes.Status500InternalServerError,
            ApiErrorWriter.InternalError,
            "The project could not be changed."),
    };

    private static IResult NotFound(HttpContext context) =>
        ApiErrorWriter.Create(
            context,
            StatusCodes.Status404NotFound,
            ProjectErrorCodes.NotFound,
            "The project does not exist.");

    private static bool TryReadProjectId(HttpContext context, out Guid projectId) =>
        Guid.TryParse(context.Request.RouteValues["id"] as string, out projectId);

    private static string ReadUserReference(HttpContext context) =>
        context.Request.RouteValues["user_id"] as string ?? string.Empty;

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

    private static bool TryParseVisibility(string? value, out ProjectVisibility visibility)
    {
        visibility = ProjectVisibility.Private;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "public":
                visibility = ProjectVisibility.Public;
                return true;
            case "private":
                visibility = ProjectVisibility.Private;
                return true;
            case "matched":
                visibility = ProjectVisibility.Matched;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseStatus(string? value, out ProjectStatus status)
    {
        status = ProjectStatus.Draft;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "active":
                status = ProjectStatus.Active;
                return true;
            case "paused":
                status = ProjectStatus.Paused;
                return true;
            case "archived":
                status = ProjectStatus.Archived;
                return true;
            default:
                return false;
        }
    }

    private static bool TryParseMemberRole(string? value, out ProjectMemberRole role)
    {
        role = ProjectMemberRole.Viewer;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "viewer":
                role = ProjectMemberRole.Viewer;
                return true;
            case "editor":
                role = ProjectMemberRole.Editor;
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
