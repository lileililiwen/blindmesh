using System.ComponentModel.DataAnnotations;

namespace Blindmesh.Domain.Findings;

/// <summary>
/// The evidence of one seat of one task. A submission belongs to a
/// participation and keeps the visibility of the task of that participation.
/// It holds the summary of the work, the flag of the completed scope, the
/// revisions the contributor sent and the findings of the review. A submission
/// is never deleted; it keeps its revisions and its history.
/// </summary>
public sealed class Submission
{
    public Guid Id { get; set; }

    /// <summary>The seat the evidence belongs to.</summary>
    public Guid ParticipationId { get; set; }

    /// <summary>The task of the seat.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The project of the task. It is the outer limit of the visibility.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The contributor that wrote the evidence.</summary>
    public Guid ContributorId { get; set; }

    /// <summary>The owner of the task that reviews the evidence.</summary>
    public Guid OwnerId { get; set; }

    public SubmissionState State { get; set; } = SubmissionState.Draft;

    /// <summary>The summary of the current revision.</summary>
    [MaxLength(5000)]
    public string Summary { get; set; } = string.Empty;

    /// <summary>True when the contributor completed the whole scope of the seat.</summary>
    public bool ScopeCompleted { get; set; }

    /// <summary>The number of the current revision. It starts at one.</summary>
    public int RevisionNumber { get; set; }

    /// <summary>The moment the evidence was submitted.</summary>
    public DateTimeOffset? SubmittedAt { get; set; }

    /// <summary>The moment the owner answered the evidence.</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>The moment the owner asked for the one correction.</summary>
    public DateTimeOffset? CorrectionRequestedAt { get; set; }

    /// <summary>The reason of the dispute between the owner and the contributor.</summary>
    [MaxLength(2000)]
    public string? DisputeReason { get; set; }

    /// <summary>The moment the correction of the contributor is due.</summary>
    public DateTimeOffset? CorrectionDeadline { get; set; }

    /// <summary>The bonus the owner granted on top of the base reward.</summary>
    public long BonusGranted { get; set; }

    /// <summary>The credits the settlement moved to the contributor.</summary>
    public long SettledCredits { get; set; }

    /// <summary>True when the acceptance came from the sweep of the deadlines.</summary>
    public bool AutoAccepted { get; set; }

    /// <summary>The optimistic concurrency version of the submission.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One revision of a submission. A revision is written once and is never
/// changed, so the evidence the owner reviewed stays readable even when the
/// contributor sends a correction.
/// </summary>
public sealed class SubmissionRevision
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    /// <summary>The number of the revision. It starts at one and grows by one.</summary>
    public int Number { get; set; }

    [MaxLength(5000)]
    public string Summary { get; set; } = string.Empty;

    public bool ScopeCompleted { get; set; }

    /// <summary>
    /// The result of every step of the task of a verification. It holds a JSON
    /// array of the objects with the fields position, title and result.
    /// </summary>
    public string StepResults { get; set; } = "[]";

    /// <summary>
    /// The areas an exploration covered. It holds a JSON array of the strings.
    /// </summary>
    public string TestedAreas { get; set; } = "[]";

    /// <summary>
    /// The explanation of a clean result. It is empty when the revision carries
    /// a finding.
    /// </summary>
    [MaxLength(5000)]
    public string CleanResultExplanation { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// One applied transition of the lifecycle of a submission. The history is
/// written in the same transaction as the submission, so it never misses a
/// state.
/// </summary>
public sealed class SubmissionStatusChange
{
    public Guid Id { get; set; }

    public Guid SubmissionId { get; set; }

    [MaxLength(16)]
    public string FromStatus { get; set; } = string.Empty;

    [MaxLength(16)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
