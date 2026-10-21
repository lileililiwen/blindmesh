namespace Blindmesh.Domain.Reputation;

/// <summary>
/// The score of a contributor at one moment. The row is append only: every
/// change of the score appends a snapshot and never rewrites an earlier one.
/// </summary>
public sealed class ReputationSnapshot
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int CurrentScore { get; set; }

    public int AcceptedContributions { get; set; }

    public int ConfirmedFindings { get; set; }

    public int ReversedContributions { get; set; }

    public DateTimeOffset TakenAt { get; set; }
}
