using System.ComponentModel.DataAnnotations;

namespace Blindmesh.Domain.Findings;

/// <summary>
/// One finding of a revision of a submission. A finding is classified on its
/// own: the decision of the owner about a finding never decides the earned
/// base pay of the submission it belongs to.
/// </summary>
public sealed class Finding
{
    public Guid Id { get; set; }

    /// <summary>The submission the finding belongs to.</summary>
    public Guid SubmissionId { get; set; }

    /// <summary>The revision the finding was reported in.</summary>
    public Guid RevisionId { get; set; }

    /// <summary>The contributor that reported the finding.</summary>
    public Guid ContributorId { get; set; }

    public FindingCategory Category { get; set; }

    public FindingSeverity Severity { get; set; }

    [MaxLength(140)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(10000)]
    public string Reproduction { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string ExpectedResult { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string ActualResult { get; set; } = string.Empty;

    public FindingState State { get; set; } = FindingState.Submitted;

    /// <summary>The note of the owner about the decision.</summary>
    [MaxLength(1000)]
    public string? DecisionNote { get; set; }

    /// <summary>The owner that classified the finding.</summary>
    public Guid? DecidedBy { get; set; }

    /// <summary>The moment the owner classified the finding.</summary>
    public DateTimeOffset? DecidedAt { get; set; }

    /// <summary>The optimistic concurrency version of the finding.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// One applied transition of the lifecycle of a finding. The history is written
/// in the same transaction as the finding, so it never misses a state.
/// </summary>
public sealed class FindingHistoryEntry
{
    public Guid Id { get; set; }

    public Guid FindingId { get; set; }

    [MaxLength(16)]
    public string FromStatus { get; set; } = string.Empty;

    [MaxLength(16)]
    public string ToStatus { get; set; } = string.Empty;

    public Guid ChangedBy { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
