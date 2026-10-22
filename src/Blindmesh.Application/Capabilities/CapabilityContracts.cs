using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The actor of a capability command. A visitor without a session is an
/// anonymous actor: it may read a public capability and nothing else.
/// </summary>
public sealed record CapabilityActor(Guid? UserId, string Username)
{
    public static CapabilityActor Anonymous { get; } = new(null, string.Empty);

    public bool IsAuthenticated => UserId.HasValue;
}

/// <summary>One applied transition of the lifecycle of a capability.</summary>
public sealed record CapabilityStatusChangeView(
    string FromStatus,
    string ToStatus,
    string ChangedBy,
    DateTimeOffset ChangedAt);

/// <summary>
/// The committed state of a capability with its tags, its metadata and the
/// history of its lifecycle.
/// </summary>
public sealed record CapabilityView(
    Guid Id,
    string Kind,
    string Label,
    string Visibility,
    bool Available,
    string Status,
    string Verification,
    string OwnerUsername,
    IReadOnlyList<string> Tags,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyList<CapabilityStatusChangeView> StatusHistory,
    bool UsableForMatching,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// One hard condition and the outcome of its match. The label and the tag of
/// the public declaration that satisfied the condition are part of the answer
/// when the condition was evaluated for another account; they are null when
/// the condition was evaluated for the owner of the declarations.
/// </summary>
public sealed record CapabilityConditionOutcome(
    string Kind,
    string Value,
    bool Satisfied,
    string Reason,
    string? DeclarationLabel = null,
    string? DeclarationTag = null);

/// <summary>
/// The outcome of an eligibility evaluation. It holds the decision and the
/// reason of every unmet condition, so the caller can act on it.
/// </summary>
public sealed record CapabilityEligibilityView(
    bool Eligible,
    IReadOnlyList<CapabilityConditionOutcome> Conditions,
    int MatchableDeclarations);

/// <summary>
/// A declaration of the contributor that may be used for a match, with its
/// tags. A private or an unavailable declaration never reaches a match.
/// </summary>
public sealed record MatchableCapability(Capability Capability, IReadOnlyList<string> Tags);

/// <summary>The outcome of a capability command.</summary>
public enum CapabilityCommandStatus
{
    /// <summary>The command changed the capability.</summary>
    Succeeded,

    /// <summary>The command created the capability.</summary>
    Created,

    /// <summary>The capability does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The capability is visible but the actor may not run the command.</summary>
    Forbidden,

    /// <summary>One field of the command is not valid.</summary>
    ValidationFailed,

    /// <summary>The expected version of the capability is not the current one.</summary>
    VersionConflict,

    /// <summary>The state of the capability does not accept the command.</summary>
    StateConflict,
}

/// <summary>
/// The answer of a capability command. The current version is part of the
/// answer of a version conflict, so the caller can reload the capability.
/// </summary>
public sealed record CapabilityCommandResult(
    CapabilityCommandStatus Status,
    CapabilityView? Capability = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    int? CurrentVersion = null,
    string? Message = null);

/// <summary>The outcome of an eligibility evaluation.</summary>
public enum CapabilityEvaluationStatus
{
    /// <summary>The evaluation ran and holds the decision.</summary>
    Evaluated,

    /// <summary>One condition of the request is not valid.</summary>
    ValidationFailed,

    /// <summary>The task of the request does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The actor may not run the evaluation.</summary>
    Forbidden,
}

/// <summary>
/// The answer of an eligibility evaluation. The evaluation never trusts the
/// data of the client: it is computed from the committed state of the
/// declarations of the contributor.
/// </summary>
public sealed record CapabilityEvaluationResult(
    CapabilityEvaluationStatus Status,
    CapabilityEligibilityView? Evaluation = null,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null);
