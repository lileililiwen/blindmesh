using System.Globalization;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Projects;

namespace Blindmesh.Application.Projects;

/// <summary>
/// The service of the projects module. It owns the lifecycle of a project, the
/// visibility of a project and the membership of a project. The actor policy
/// is checked again on every command, so a change of the membership or of the
/// visibility is honoured by the next read.
/// </summary>
public sealed class ProjectService(IProjectStore store, IClock clock, ITaskMatchSeam match)
{
    /// <summary>What the actor may do with a project.</summary>
    private enum AccessLevel
    {
        /// <summary>The actor may read the project.</summary>
        Reader,

        /// <summary>The actor may read the project and change its content.</summary>
        Editor,

        /// <summary>The actor owns the project and manages its members.</summary>
        Owner,
    }

    /// <summary>
    /// The project and what the actor may do with it. The access is null when
    /// the project may not be seen by the actor.
    /// </summary>
    private sealed record ProjectAccess(Project Project, AccessLevel Level)
    {
        /// <summary>True when the actor is the owner or a member of the project.</summary>
        public bool IsInsider { get; init; }
    }

    /// <summary>
    /// The projects the actor may see, in the stable descending order of the
    /// list envelope. A project of the matched scope is listed for an account
    /// that fits one of its open tasks, and for nobody else.
    /// </summary>
    public async Task<PagedResult<ProjectSummaryView>> ListAsync(
        ProjectActor actor,
        int limit,
        ProjectCursor? cursor,
        CancellationToken cancellationToken)
    {
        var page = await store.ListVisibleAsync(
            actor.UserId,
            new ProjectListQuery(limit, cursor),
            cancellationToken);

        // The store returns the projects of the matched scope as a superset.
        // The service keeps the ones the account fits an open task of, and it
        // keeps the ones of the insiders of the project, because an insider
        // reads everything of its project.
        var visible = new List<Project>();

        foreach (var project in page.Items)
        {
            if (project.Visibility != ProjectVisibility.Matched)
            {
                visible.Add(project);
                continue;
            }

            if (actor.UserId is not Guid userId)
            {
                continue;
            }

            if (await IsInsiderAsync(project.Id, userId, cancellationToken) ||
                await match.MatchingTaskIdsAsync(project.Id, userId, cancellationToken) is { Count: > 0 })
            {
                visible.Add(project);
            }
        }

        if (visible.Count == 0)
        {
            return new PagedResult<ProjectSummaryView>
            {
                Items = [],
                NextCursor = page.Next?.Encode(),
            };
        }

        var ownerIds = visible.Select(project => project.OwnerId).Distinct().ToArray();
        var usernames = await store.ListUsernamesAsync(ownerIds, cancellationToken);

        var items = visible
            .Select(project => new ProjectSummaryView(
                project.Id,
                project.Name,
                project.Summary,
                StatusText(project.Status),
                VisibilityText(project.Visibility),
                usernames.GetValueOrDefault(project.OwnerId, string.Empty),
                project.Version,
                project.CreatedAt,
                project.UpdatedAt))
            .ToArray();

        return new PagedResult<ProjectSummaryView>
        {
            Items = items,
            NextCursor = page.Next?.Encode(),
        };
    }

    /// <summary>
    /// The committed state of a project. A project that the actor may not see
    /// is not found, so the answer never reveals that it exists. A project of
    /// the matched scope is read by an account that fits one of its open
    /// tasks.
    /// </summary>
    public async Task<ProjectCommandResult> GetAsync(
        ProjectActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            var matched = await LoadMatchedAsync(actor, projectId, cancellationToken);

            if (matched is null)
            {
                return NotFound();
            }

            return new ProjectCommandResult(
                ProjectCommandStatus.Succeeded,
                await ToDetailAsync(matched, cancellationToken));
        }

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// The access of the actor to a project. Null means that the project may
    /// not be seen by the actor at all, so a child object of the project must
    /// not be found either. The method is the seam of the project module for
    /// the modules that keep the visibility of a child object, such as the
    /// tasks module.
    /// </summary>
    public async Task<ProjectAccessView?> GetAccessAsync(
        ProjectActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return null;
        }

        return new ProjectAccessView(
            access.Level == AccessLevel.Owner,
            access.IsInsider,
            access.Level is AccessLevel.Owner or AccessLevel.Editor,
            access.Project.Status == ProjectStatus.Active,
            VisibilityText(access.Project.Visibility),
            StatusText(access.Project.Status));
    }

    /// <summary>
    /// The reference of a project for a reader. It is null when the actor may
    /// not see the project, so the title of a private project never reaches a
    /// reader that may not read it. The modules that keep the visibility of a
    /// child object read the visibility of the project through this answer.
    /// </summary>
    public async Task<ProjectReferenceView?> GetReferenceAsync(
        ProjectActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return null;
        }

        return new ProjectReferenceView(access.Project.Id, access.Project.Name);
    }

    /// <summary>
    /// Creates a project in the draft state. The owner of a new project is the
    /// actor; the owner is the only member manager of the project.
    /// </summary>
    public async Task<ProjectCommandResult> CreateAsync(
        ProjectActor actor,
        ProjectInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "A signed in contributor is required to create a project.");
        }

        var errors = ProjectRules.Validate(input);

        if (errors.Count > 0)
        {
            return new ProjectCommandResult(ProjectCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var now = clock.UtcNow;

        var project = new Project
        {
            Id = Guid.NewGuid(),
            OwnerId = actor.UserId!.Value,
            Name = input.Name.Trim(),
            Summary = input.Summary.Trim(),
            Description = input.Description.Trim(),
            RepositoryUrl = input.RepositoryUrl.Trim(),
            Visibility = input.Visibility,
            Status = ProjectStatus.Draft,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await store.AddProjectAsync(project, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        return new ProjectCommandResult(
            ProjectCommandStatus.Created,
            await ToDetailAsync(
                new ProjectAccess(project, AccessLevel.Owner) { IsInsider = true },
                cancellationToken));
    }

    /// <summary>
    /// Saves the content of a project. The owner and the editors may change
    /// the content; only the owner may change the visibility.
    /// </summary>
    public async Task<ProjectCommandResult> UpdateAsync(
        ProjectActor actor,
        Guid projectId,
        ProjectPatch patch,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        var changesVisibility = patch.Visibility is not null &&
            patch.Visibility.Value != access.Project.Visibility;

        if (changesVisibility && access.Level != AccessLevel.Owner)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner of the project changes its visibility.");
        }

        if (access.Level is not (AccessLevel.Owner or AccessLevel.Editor))
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner and the editors of the project change its content.");
        }

        if (access.Project.Status == ProjectStatus.Archived)
        {
            return Archived();
        }

        if (access.Project.Version != expectedVersion)
        {
            return Conflict(access.Project.Version);
        }

        var input = new ProjectInput
        {
            Name = patch.Name ?? access.Project.Name,
            Summary = patch.Summary ?? access.Project.Summary,
            Description = patch.Description ?? access.Project.Description,
            RepositoryUrl = patch.RepositoryUrl ?? access.Project.RepositoryUrl,
            Visibility = patch.Visibility ?? access.Project.Visibility,
        };

        var errors = ProjectRules.Validate(input);

        if (errors.Count > 0)
        {
            return new ProjectCommandResult(ProjectCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var project = access.Project;

        project.Name = input.Name.Trim();
        project.Summary = input.Summary.Trim();
        project.Description = input.Description.Trim();
        project.RepositoryUrl = input.RepositoryUrl.Trim();
        project.Visibility = input.Visibility;
        project.Version += 1;
        project.UpdatedAt = clock.UtcNow;

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return await ConflictAsync(projectId, cancellationToken);
        }

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Applies a transition of the lifecycle of a project. Every applied
    /// transition is recorded in the status history of the project.
    /// </summary>
    public async Task<ProjectCommandResult> TransitionAsync(
        ProjectActor actor,
        Guid projectId,
        ProjectStatus target,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        var allowed = target switch
        {
            ProjectStatus.Active => access.Level is AccessLevel.Owner or AccessLevel.Editor,
            ProjectStatus.Paused or ProjectStatus.Archived => access.Level == AccessLevel.Owner,
            _ => false,
        };

        if (!allowed)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner of the project pauses and archives it.");
        }

        if (access.Project.Version != expectedVersion)
        {
            return Conflict(access.Project.Version);
        }

        if (!ProjectRules.IsAllowedTransition(access.Project.Status, target))
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.InvalidTransition,
                Message: $"The project cannot move from {StatusText(access.Project.Status)} to {StatusText(target)}.");
        }

        var project = access.Project;
        var from = project.Status;
        var now = clock.UtcNow;

        project.Status = target;
        project.Version += 1;
        project.UpdatedAt = now;

        await store.AddStatusChangeAsync(
            new ProjectStatusChange
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                FromStatus = from,
                ToStatus = target,
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return await ConflictAsync(projectId, cancellationToken);
        }

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Adds a member to a project. The owner is the only member manager; a
    /// member may view and apply for the work of the project but never
    /// manages it.
    /// </summary>
    public async Task<ProjectCommandResult> AddMemberAsync(
        ProjectActor actor,
        Guid projectId,
        string userReference,
        ProjectMemberRole role,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (access.Level != AccessLevel.Owner)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner of the project manages its members.");
        }

        var userId = await store.ResolveUserIdAsync(userReference, cancellationToken);

        if (userId is null)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["username"] = ["There is no contributor with this name."],
                });
        }

        if (userId.Value == access.Project.OwnerId)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["username"] = ["The owner of the project is not a member row."],
                });
        }

        var member = await store.FindMemberAsync(projectId, userId.Value, cancellationToken);

        if (member is null)
        {
            await store.AddMemberAsync(
                new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    UserId = userId.Value,
                    Role = role,
                    CreatedAt = clock.UtcNow,
                },
                cancellationToken);
        }
        else
        {
            // The same member may be given another role.
            member.Role = role;
        }

        await store.SaveChangesAsync(cancellationToken);

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Removes a member of a project. The next read of the project honours
    /// the removed membership.
    /// </summary>
    public async Task<ProjectCommandResult> RemoveMemberAsync(
        ProjectActor actor,
        Guid projectId,
        string userReference,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (access.Level != AccessLevel.Owner)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner of the project manages its members.");
        }

        var userId = await store.ResolveUserIdAsync(userReference, cancellationToken);

        if (userId is null)
        {
            return NotFound();
        }

        if (userId.Value == access.Project.OwnerId)
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.ValidationFailed,
                FieldErrors: new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["username"] = ["The owner of the project cannot be removed."],
                });
        }

        if (!await store.RemoveMemberAsync(projectId, userId.Value, cancellationToken))
        {
            return NotFound();
        }

        await store.SaveChangesAsync(cancellationToken);

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    /// <summary>
    /// Records a released version of a project. The owner and the editors may
    /// record a version; an archived project accepts no new version.
    /// </summary>
    public async Task<ProjectCommandResult> AddVersionAsync(
        ProjectActor actor,
        Guid projectId,
        string? label,
        string? commitReference,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, projectId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (access.Level is not (AccessLevel.Owner or AccessLevel.Editor))
        {
            return new ProjectCommandResult(
                ProjectCommandStatus.Forbidden,
                Message: "Only the owner and the editors of the project record a version.");
        }

        if (access.Project.Status == ProjectStatus.Archived)
        {
            return Archived();
        }

        var errors = ProjectRules.ValidateVersion(label, commitReference);

        if (errors.Count > 0)
        {
            return new ProjectCommandResult(ProjectCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var project = access.Project;
        var now = clock.UtcNow;

        await store.AddVersionAsync(
            new ProjectVersion
            {
                Id = Guid.NewGuid(),
                ProjectId = project.Id,
                Label = label!.Trim(),
                CommitReference = string.IsNullOrWhiteSpace(commitReference) ? null : commitReference.Trim(),
                CreatedBy = actor.UserId!.Value,
                CreatedAt = now,
            },
            cancellationToken);

        project.Version += 1;
        project.UpdatedAt = now;

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return await ConflictAsync(projectId, cancellationToken);
        }

        return new ProjectCommandResult(
            ProjectCommandStatus.Succeeded,
            await ToDetailAsync(access, cancellationToken));
    }

    private async Task<ProjectAccess?> LoadAsync(
        ProjectActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await store.FindProjectAsync(projectId, cancellationToken);

        if (project is null)
        {
            return null;
        }

        if (actor.UserId is Guid userId)
        {
            if (project.OwnerId == userId)
            {
                return new ProjectAccess(project, AccessLevel.Owner) { IsInsider = true };
            }

            var member = await store.FindMemberAsync(projectId, userId, cancellationToken);

            if (member is not null)
            {
                var level = member.Role == ProjectMemberRole.Editor
                    ? AccessLevel.Editor
                    : AccessLevel.Reader;

                return new ProjectAccess(project, level) { IsInsider = true };
            }
        }

        if (project.Visibility == ProjectVisibility.Public)
        {
            return new ProjectAccess(project, AccessLevel.Reader);
        }

        return null;
    }

    /// <summary>
    /// True when the account is an insider of the project: the owner of it or a
    /// member of it. The answer does not depend on the visibility of the
    /// project, so it holds for a project of the matched scope too.
    /// </summary>
    public async Task<bool> IsInsiderAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var project = await store.FindProjectAsync(projectId, cancellationToken);

        if (project is null)
        {
            return false;
        }

        if (project.OwnerId == userId)
        {
            return true;
        }

        return await store.FindMemberAsync(projectId, userId, cancellationToken) is not null;
    }

    /// <summary>
    /// The access of an account that is not an insider of a project of the
    /// matched scope. The project is read by an account that fits one of its
    /// open tasks, and by nobody else. The rule of the scope of the matching
    /// module decides, so the project and the task never disagree.
    /// </summary>
    private async Task<ProjectAccess?> LoadMatchedAsync(
        ProjectActor actor,
        Guid projectId,
        CancellationToken cancellationToken)
    {
        if (actor.UserId is not Guid userId)
        {
            return null;
        }

        var project = await store.FindProjectAsync(projectId, cancellationToken);

        if (project is null || project.Visibility != ProjectVisibility.Matched)
        {
            return null;
        }

        var matching = await match.MatchingTaskIdsAsync(projectId, userId, cancellationToken);

        if (matching.Count == 0)
        {
            return null;
        }

        return new ProjectAccess(project, AccessLevel.Reader);
    }

    private async Task<ProjectDetailView> ToDetailAsync(ProjectAccess access, CancellationToken cancellationToken)
    {
        var project = access.Project;

        var members = access.IsInsider
            ? await store.ListMembersAsync(project.Id, cancellationToken)
            : [];

        var versions = await store.ListVersionsAsync(project.Id, cancellationToken);
        var history = await store.ListStatusHistoryAsync(project.Id, cancellationToken);

        var userIds = members
            .Select(member => member.UserId)
            .Concat(versions.Select(version => version.CreatedBy))
            .Concat(history.Select(change => change.ChangedBy))
            .Append(project.OwnerId)
            .Distinct()
            .ToArray();

        var usernames = await store.ListUsernamesAsync(userIds, cancellationToken);

        string NameOf(Guid userId) => usernames.GetValueOrDefault(userId, string.Empty);

        return new ProjectDetailView(
            project.Id,
            project.Name,
            project.Summary,
            project.Description,
            project.RepositoryUrl,
            StatusText(project.Status),
            VisibilityText(project.Visibility),
            project.Version,
            project.Status == ProjectStatus.Active,
            NameOf(project.OwnerId),
            members
                .Select(member => new ProjectMemberView(
                    member.UserId,
                    NameOf(member.UserId),
                    MemberRoleText(member.Role),
                    member.CreatedAt))
                .ToArray(),
            versions
                .Select(version => new ProjectVersionView(
                    version.Id,
                    version.Label,
                    version.CommitReference,
                    NameOf(version.CreatedBy),
                    version.CreatedAt))
                .ToArray(),
            history
                .Select(change => new ProjectStatusChangeView(
                    StatusText(change.FromStatus),
                    StatusText(change.ToStatus),
                    NameOf(change.ChangedBy),
                    change.ChangedAt))
                .ToArray(),
            project.CreatedAt,
            project.UpdatedAt);
    }

    private async Task<ProjectCommandResult> ConflictAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var current = await store.FindProjectAsync(projectId, cancellationToken);

        return Conflict(current?.Version);
    }

    private static ProjectCommandResult NotFound() =>
        new ProjectCommandResult(ProjectCommandStatus.NotFound, Message: "The project does not exist.");

    private static ProjectCommandResult Conflict(int? currentVersion)
    {
        var message = "The project was changed somewhere else. The current version is " +
            (currentVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown") + ".";

        if (currentVersion is null)
        {
            message = "The project was changed somewhere else. Load the current project before saving again.";
        }

        return new ProjectCommandResult(
            ProjectCommandStatus.VersionConflict,
            CurrentVersion: currentVersion,
            Message: message);
    }

    private static ProjectCommandResult Archived() =>
        new ProjectCommandResult(
            ProjectCommandStatus.StateConflict,
            Message: "The project is archived and does not accept changes.");

    /// <summary>The wire name of a lifecycle state.</summary>
    public static string StatusText(ProjectStatus status) => status switch
    {
        ProjectStatus.Active => "active",
        ProjectStatus.Paused => "paused",
        ProjectStatus.Archived => "archived",
        _ => "draft",
    };

    /// <summary>The wire name of a visibility.</summary>
    public static string VisibilityText(ProjectVisibility visibility) => visibility switch
    {
        ProjectVisibility.Public => "public",
        ProjectVisibility.Matched => "matched",
        _ => "private",
    };

    /// <summary>The wire name of a member role.</summary>
    public static string MemberRoleText(ProjectMemberRole role) => role switch
    {
        ProjectMemberRole.Editor => "editor",
        _ => "viewer",
    };
}
