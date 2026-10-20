using System.Globalization;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The service of the capabilities module. It owns the lifecycle of a
/// declaration, its visibility and the deterministic evaluation of the hard
/// conditions of a task. The actor policy is checked again on every command, so
/// a change of the visibility is honoured by the next read.
/// </summary>
public sealed class CapabilityService(
    ICapabilityStore store,
    IClock clock,
    ITaskConditionReader taskConditions)
{
    /// <summary>
    /// The capabilities the actor may see in the directory, in the stable
    /// descending order of the list envelope.
    /// </summary>
    public async Task<PagedResult<CapabilityView>> ListAsync(
        CapabilityActor actor,
        int limit,
        CapabilityCursor? cursor,
        CancellationToken cancellationToken)
    {
        var page = await store.ListVisibleAsync(
            actor.UserId,
            new CapabilityListQuery(limit, cursor),
            cancellationToken);

        return new PagedResult<CapabilityView>
        {
            Items = await ToViewsAsync(page.Items, cancellationToken),
            NextCursor = page.Next?.Encode(),
        };
    }

    /// <summary>
    /// The declarations of the actor, including the withdrawn ones. A withdrawn
    /// declaration keeps its row and its history.
    /// </summary>
    public async Task<PagedResult<CapabilityView>> ListOwnedAsync(
        CapabilityActor actor,
        int limit,
        CapabilityCursor? cursor,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new PagedResult<CapabilityView> { Items = [] };
        }

        var page = await store.ListOwnedAsync(
            actor.UserId!.Value,
            new CapabilityListQuery(limit, cursor),
            cancellationToken);

        return new PagedResult<CapabilityView>
        {
            Items = await ToViewsAsync(page.Items, cancellationToken),
            NextCursor = page.Next?.Encode(),
        };
    }

    /// <summary>
    /// The committed state of a capability. A capability that the actor may not
    /// see is not found, so the answer never reveals that it exists.
    /// </summary>
    public async Task<CapabilityCommandResult> GetAsync(
        CapabilityActor actor,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, capabilityId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        return new CapabilityCommandResult(
            CapabilityCommandStatus.Succeeded,
            (await ToViewsAsync([access], cancellationToken))[0]);
    }

    /// <summary>
    /// Creates a declaration for the actor. The verification of a new
    /// declaration is always self declared; only the evidence of a moderator
    /// may change it and the moderation package is a later package.
    /// </summary>
    public async Task<CapabilityCommandResult> CreateAsync(
        CapabilityActor actor,
        CapabilityInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!actor.IsAuthenticated)
        {
            return new CapabilityCommandResult(
                CapabilityCommandStatus.Forbidden,
                Message: "A signed in contributor is required to declare a capability.");
        }

        var errors = CapabilityRules.Validate(input);

        if (errors.Count > 0)
        {
            return new CapabilityCommandResult(CapabilityCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var now = clock.UtcNow;

        var capability = new Capability
        {
            Id = Guid.NewGuid(),
            OwnerId = actor.UserId!.Value,
            Kind = input.Kind,
            Label = input.Label.Trim(),
            Visibility = input.Visibility,
            Available = input.Available,
            Status = CapabilityStatus.Declared,
            Verification = CapabilityVerification.SelfDeclared,
            Metadata = NormalizeMetadata(input.Metadata),
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await store.AddCapabilityAsync(capability, cancellationToken);

        foreach (var tag in input.Tags)
        {
            await store.AddTagAsync(
                new CapabilityTag
                {
                    Id = Guid.NewGuid(),
                    CapabilityId = capability.Id,
                    Tag = CapabilityRules.Normalize(tag),
                },
                cancellationToken);
        }

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return new CapabilityCommandResult(
                CapabilityCommandStatus.StateConflict,
                Message: "The declaration could not be stored. Try again.");
        }

        return new CapabilityCommandResult(
            CapabilityCommandStatus.Created,
            (await ToViewsAsync([capability], cancellationToken))[0]);
    }

    /// <summary>
    /// Saves the content of a declaration. Only the owner of a declaration
    /// changes it, and a withdrawn declaration accepts no change.
    /// </summary>
    public async Task<CapabilityCommandResult> UpdateAsync(
        CapabilityActor actor,
        Guid capabilityId,
        CapabilityPatch patch,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(patch);

        var access = await LoadAsync(actor, capabilityId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (access.OwnerId != actor.UserId)
        {
            return new CapabilityCommandResult(
                CapabilityCommandStatus.Forbidden,
                Message: "Only the owner of a capability changes it.");
        }

        if (access.Status == CapabilityStatus.Withdrawn)
        {
            return Withdrawn();
        }

        if (access.Version != expectedVersion)
        {
            return Conflict(access.Version);
        }

        var currentTags = await store.ListTagsAsync([capabilityId], cancellationToken);

        var input = new CapabilityInput
        {
            Kind = patch.Kind ?? access.Kind,
            Label = patch.Label ?? access.Label,
            Tags = patch.Tags ?? currentTags.Select(tag => tag.Tag).ToArray(),
            Visibility = patch.Visibility ?? access.Visibility,
            Available = patch.Available ?? access.Available,
            Metadata = patch.Metadata ?? access.Metadata,
        };

        var errors = CapabilityRules.Validate(input);

        if (errors.Count > 0)
        {
            return new CapabilityCommandResult(CapabilityCommandStatus.ValidationFailed, FieldErrors: errors);
        }

        var capability = access;

        capability.Kind = input.Kind;
        capability.Label = input.Label.Trim();
        capability.Visibility = input.Visibility;
        capability.Available = input.Available;
        capability.Metadata = NormalizeMetadata(input.Metadata);
        capability.Version += 1;
        capability.UpdatedAt = clock.UtcNow;

        await store.RemoveTagsAsync(capabilityId, cancellationToken);

        foreach (var tag in input.Tags)
        {
            await store.AddTagAsync(
                new CapabilityTag
                {
                    Id = Guid.NewGuid(),
                    CapabilityId = capabilityId,
                    Tag = CapabilityRules.Normalize(tag),
                },
                cancellationToken);
        }

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return await ConflictAsync(capabilityId, cancellationToken);
        }

        return new CapabilityCommandResult(
            CapabilityCommandStatus.Succeeded,
            (await ToViewsAsync([capability], cancellationToken))[0]);
    }

    /// <summary>
    /// Withdraws a declaration. The declaration keeps its row and its history,
    /// but it never matches a condition again.
    /// </summary>
    public async Task<CapabilityCommandResult> WithdrawAsync(
        CapabilityActor actor,
        Guid capabilityId,
        int expectedVersion,
        CancellationToken cancellationToken)
    {
        var access = await LoadAsync(actor, capabilityId, cancellationToken);

        if (access is null)
        {
            return NotFound();
        }

        if (access.OwnerId != actor.UserId)
        {
            return new CapabilityCommandResult(
                CapabilityCommandStatus.Forbidden,
                Message: "Only the owner of a capability withdraws it.");
        }

        if (access.Status == CapabilityStatus.Withdrawn)
        {
            return new CapabilityCommandResult(
                CapabilityCommandStatus.StateConflict,
                Message: "The capability is already withdrawn.");
        }

        if (access.Version != expectedVersion)
        {
            return Conflict(access.Version);
        }

        var now = clock.UtcNow;
        var from = access.Status;

        access.Status = CapabilityStatus.Withdrawn;
        access.Version += 1;
        access.UpdatedAt = now;

        await store.AddStatusChangeAsync(
            new CapabilityStatusChange
            {
                Id = Guid.NewGuid(),
                CapabilityId = capabilityId,
                FromStatus = from,
                ToStatus = CapabilityStatus.Withdrawn,
                ChangedBy = actor.UserId!.Value,
                ChangedAt = now,
            },
            cancellationToken);

        if (!await store.SaveChangesAsync(cancellationToken))
        {
            return await ConflictAsync(capabilityId, cancellationToken);
        }

        return new CapabilityCommandResult(
            CapabilityCommandStatus.Succeeded,
            (await ToViewsAsync([access], cancellationToken))[0]);
    }

    /// <summary>
    /// Evaluates a set of hard conditions against the public and available
    /// declarations of the actor. The decision is deterministic: the same
    /// committed state and the same conditions always give the same answer.
    /// </summary>
    public async Task<CapabilityEvaluationResult> EvaluateAsync(
        CapabilityActor actor,
        IReadOnlyList<CapabilityHardCondition> conditions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        if (!actor.IsAuthenticated)
        {
            return new CapabilityEvaluationResult(
                CapabilityEvaluationStatus.Forbidden,
                Message: "A signed in contributor is required to evaluate an eligibility.");
        }

        var errors = CapabilityRules.ValidateConditions(conditions);

        if (errors.Count > 0)
        {
            return new CapabilityEvaluationResult(
                CapabilityEvaluationStatus.ValidationFailed,
                FieldErrors: errors);
        }

        var declarations = await store.ListMatchableAsync(actor.UserId!.Value, cancellationToken);

        var outcomes = new List<CapabilityConditionOutcome>(conditions.Count);

        foreach (var condition in conditions)
        {
            outcomes.Add(Evaluate(condition, declarations));
        }

        var eligible = outcomes.TrueForAll(outcome => outcome.Satisfied);

        return new CapabilityEvaluationResult(
            CapabilityEvaluationStatus.Evaluated,
            new CapabilityEligibilityView(eligible, outcomes, declarations.Count));
    }

    /// <summary>
    /// Evaluates the hard conditions of a published task. The conditions are
    /// read through the seam of the task package, so this module never invents
    /// a task and never reads a task table. A task that is not published or not
    /// visible to the actor is not found.
    /// </summary>
    public async Task<CapabilityEvaluationResult> EvaluateForTaskAsync(
        CapabilityActor actor,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        if (!actor.IsAuthenticated)
        {
            return new CapabilityEvaluationResult(
                CapabilityEvaluationStatus.Forbidden,
                Message: "A signed in contributor is required to evaluate an eligibility.");
        }

        var conditions = await taskConditions.ReadAsync(taskId, actor.UserId!.Value, cancellationToken);

        if (conditions is null)
        {
            return new CapabilityEvaluationResult(
                CapabilityEvaluationStatus.NotFound,
                Message: "The task does not exist.");
        }

        return await EvaluateAsync(actor, conditions, cancellationToken);
    }

    /// <summary>
    /// The capability and the access of the actor. The access is null when the
    /// capability may not be seen by the actor. A withdrawn capability is
    /// readable by its owner only, because it is not offered any more.
    /// </summary>
    private async Task<Capability?> LoadAsync(
        CapabilityActor actor,
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var capability = await store.FindCapabilityAsync(capabilityId, cancellationToken);

        if (capability is null)
        {
            return null;
        }

        if (actor.UserId is Guid userId && capability.OwnerId == userId)
        {
            return capability;
        }

        if (capability.Visibility != CapabilityVisibility.Public)
        {
            return null;
        }

        if (capability.Status == CapabilityStatus.Withdrawn)
        {
            return null;
        }

        return capability;
    }

    private async Task<IReadOnlyList<CapabilityView>> ToViewsAsync(
        IReadOnlyList<Capability> capabilities,
        CancellationToken cancellationToken)
    {
        if (capabilities.Count == 0)
        {
            return [];
        }

        var ids = capabilities.Select(capability => capability.Id).ToArray();

        var tags = await store.ListTagsAsync(ids, cancellationToken);
        var history = await store.ListStatusHistoryAsync(ids, cancellationToken);

        var userIds = capabilities
            .Select(capability => capability.OwnerId)
            .Concat(history.Select(change => change.ChangedBy))
            .Distinct()
            .ToArray();

        var usernames = await store.ListUsernamesAsync(userIds, cancellationToken);

        return capabilities
            .Select(capability => new CapabilityView(
                capability.Id,
                KindText(capability.Kind),
                capability.Label,
                VisibilityText(capability.Visibility),
                capability.Available,
                StatusText(capability.Status),
                VerificationText(capability.Verification),
                usernames.GetValueOrDefault(capability.OwnerId, string.Empty),
                tags
                    .Where(tag => tag.CapabilityId == capability.Id)
                    .Select(tag => tag.Tag)
                    .ToArray(),
                capability.Metadata,
                history
                    .Where(change => change.CapabilityId == capability.Id)
                    .Select(change => new CapabilityStatusChangeView(
                        StatusText(change.FromStatus),
                        StatusText(change.ToStatus),
                        usernames.GetValueOrDefault(change.ChangedBy, string.Empty),
                        change.ChangedAt))
                    .ToArray(),
                IsMatchable(capability),
                capability.Version,
                capability.CreatedAt,
                capability.UpdatedAt))
            .ToArray();
    }

    private static CapabilityConditionOutcome Evaluate(
        CapabilityHardCondition condition,
        IReadOnlyList<MatchableCapability> declarations)
    {
        var kind = KindText(condition.Kind);
        var value = condition.Value.Trim();

        foreach (var declaration in declarations)
        {
            if (declaration.Capability.Kind != condition.Kind)
            {
                continue;
            }

            if (!CapabilityRules.Matches(declaration.Capability.Label, declaration.Tags, value))
            {
                continue;
            }

            return new CapabilityConditionOutcome(
                kind,
                value,
                true,
                $"Your public {kind} declaration '{declaration.Capability.Label}' matches the required {kind} '{value}'.");
        }

        return new CapabilityConditionOutcome(
            kind,
            value,
            false,
            $"You have no public and available {kind} declaration that matches '{value}'. " +
            $"Declare one, or make your {kind} declaration public and available, so it can be matched.");
    }

    /// <summary>True when a declaration may be used for a match.</summary>
    private static bool IsMatchable(Capability capability) =>
        capability.Visibility == CapabilityVisibility.Public &&
        capability.Available &&
        capability.Status == CapabilityStatus.Declared;

    private static Dictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in metadata)
        {
            normalized[entry.Key.Trim().ToLowerInvariant()] = entry.Value.Trim();
        }

        return normalized;
    }

    private async Task<CapabilityCommandResult> ConflictAsync(
        Guid capabilityId,
        CancellationToken cancellationToken)
    {
        var current = await store.FindCapabilityAsync(capabilityId, cancellationToken);

        return Conflict(current?.Version);
    }

    private static CapabilityCommandResult NotFound() =>
        new CapabilityCommandResult(CapabilityCommandStatus.NotFound, Message: "The capability does not exist.");

    private static CapabilityCommandResult Conflict(int? currentVersion)
    {
        var message = "The capability was changed somewhere else. The current version is " +
            (currentVersion?.ToString(CultureInfo.InvariantCulture) ?? "unknown") + ".";

        if (currentVersion is null)
        {
            message = "The capability was changed somewhere else. Load the current capability before saving again.";
        }

        return new CapabilityCommandResult(
            CapabilityCommandStatus.VersionConflict,
            CurrentVersion: currentVersion,
            Message: message);
    }

    private static CapabilityCommandResult Withdrawn() =>
        new CapabilityCommandResult(
            CapabilityCommandStatus.StateConflict,
            Message: "The capability is withdrawn and does not accept changes.");

    /// <summary>The wire name of a kind of capability.</summary>
    public static string KindText(CapabilityKind kind) => kind switch
    {
        CapabilityKind.Account => "account",
        CapabilityKind.Device => "device",
        CapabilityKind.Skill => "skill",
        CapabilityKind.Region => "region",
        CapabilityKind.Network => "network",
        _ => "role",
    };

    /// <summary>The wire name of a visibility.</summary>
    public static string VisibilityText(CapabilityVisibility visibility) => visibility switch
    {
        CapabilityVisibility.Public => "public",
        _ => "private",
    };

    /// <summary>The wire name of a lifecycle state.</summary>
    public static string StatusText(CapabilityStatus status) => status switch
    {
        CapabilityStatus.Withdrawn => "withdrawn",
        _ => "declared",
    };

    /// <summary>The wire name of a verification state.</summary>
    public static string VerificationText(CapabilityVerification verification) => verification switch
    {
        CapabilityVerification.Verified => "verified",
        _ => "self_declared",
    };
}
