namespace Blindmesh.Domain.Reputation;

/// <summary>
/// One line of the trail of the reputation of a contributor. The row is append
/// only: a reversal never deletes the line of the acceptance it corrects, so
/// the history keeps the evidence of both.
/// </summary>
public sealed class ReputationEvent
{
    public Guid Id { get; set; }

    /// <summary>The contributor the line belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The outbox message that produced the line. It is unique, so a message
    /// that is delivered twice never counts twice.
    /// </summary>
    public Guid SourceEventId { get; set; }

    public ReputationCategory Category { get; set; }

    /// <summary>The change of the score of the contributor.</summary>
    public int Delta { get; set; }

    /// <summary>The seat the line comes from, when it is known.</summary>
    public Guid? SourceParticipationId { get; set; }

    /// <summary>The evidence of the seat the line comes from, when it is known.</summary>
    public Guid? SourceSubmissionId { get; set; }

    /// <summary>The finding the line comes from, when it is known.</summary>
    public Guid? SourceFindingId { get; set; }

    /// <summary>The project the line comes from, when it is known.</summary>
    public Guid? SourceProjectId { get; set; }

    /// <summary>The task the line comes from, when it is known.</summary>
    public Guid? SourceTaskId { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>
    /// A short and safe explanation of the line. It never holds a title, a
    /// finding text, a party or a link.
    /// </summary>
    public string Note { get; set; } = string.Empty;
}
