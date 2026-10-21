namespace Blindmesh.Domain.Reputation;

/// <summary>
/// The source of a reputation event. Every category explains where the change
/// of the score comes from, so a contributor and a visitor read the same
/// reason on the same line of the history.
/// </summary>
public enum ReputationCategory
{
    /// <summary>The work of a seat was finally accepted.</summary>
    AcceptedContribution,

    /// <summary>A finding of a contributor was confirmed by the owner of the task.</summary>
    ConfirmedFinding,

    /// <summary>An arbitration took back an accepted contribution.</summary>
    ReversedContribution,
}
