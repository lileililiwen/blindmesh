namespace Blindmesh.Domain.Moderation;

/// <summary>
/// A dispute of a seat. The row holds the case of the arbitration: the parties,
/// the escrow that waits for the decision, the decision itself and the appeal.
/// The row never holds a ledger amount that is not also written by the credit
/// module.
/// </summary>
public sealed class Dispute
{
    public Guid Id { get; set; }

    /// <summary>The submission whose seat is disputed.</summary>
    public Guid SubmissionId { get; set; }

    /// <summary>The participation of the disputed seat.</summary>
    public Guid ParticipationId { get; set; }

    /// <summary>The task that owns the seat.</summary>
    public Guid TaskId { get; set; }

    /// <summary>The project of the task.</summary>
    public Guid ProjectId { get; set; }

    /// <summary>The publisher of the task. Its escrow is at stake.</summary>
    public Guid PublisherId { get; set; }

    /// <summary>The contributor of the seat.</summary>
    public Guid ContributorId { get; set; }

    /// <summary>The report the case came from, when it came from one.</summary>
    public Guid? ReportId { get; set; }

    /// <summary>The state of the case.</summary>
    public DisputeState State { get; set; } = DisputeState.Open;

    /// <summary>The reason the publisher opened the dispute.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>The base reward of the seat. It is the amount at stake.</summary>
    public long BaseReward { get; set; }

    /// <summary>
    /// The amount the decision of the case awarded to the contributor: the base
    /// of the seat and the bonus of the decision. The appeal compensates exactly
    /// this amount, so the row never holds an amount that the credit module did
    /// not write.
    /// </summary>
    public long AwardedAmount { get; set; }

    /// <summary>True when the arbitration awarded the base to the contributor.</summary>
    public bool? AwardToContributor { get; set; }

    /// <summary>
    /// The note of the moderator that asked the parties of the case for more
    /// evidence. It is empty when no evidence was asked for.
    /// </summary>
    public string? EvidenceNote { get; set; }

    /// <summary>The note of the decision of the moderator.</summary>
    public string? ResolutionReason { get; set; }

    /// <summary>The moderator that decided the case.</summary>
    public Guid? ResolvedBy { get; set; }

    /// <summary>When the case was decided.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    /// <summary>The ledger transaction of the arbitration. It is never changed.</summary>
    public Guid? LedgerTransactionId { get; set; }

    /// <summary>
    /// The kind of the ledger transaction of the case: an arbitration of the
    /// first decision or the compensation of an appeal.
    /// </summary>
    public string? LedgerTransactionKind { get; set; }

    /// <summary>The party that appealed the decision.</summary>
    public Guid? AppealedBy { get; set; }

    /// <summary>When the decision was appealed.</summary>
    public DateTimeOffset? AppealedAt { get; set; }

    /// <summary>The reason of the appeal.</summary>
    public string? AppealReason { get; set; }

    /// <summary>The moderator that closed the case.</summary>
    public Guid? ClosedBy { get; set; }

    /// <summary>When the case was closed.</summary>
    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>The optimistic concurrency version of the case.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
