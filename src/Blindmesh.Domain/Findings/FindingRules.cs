namespace Blindmesh.Domain.Findings;

/// <summary>
/// The rules of a finding. The state machine of a finding is its own: a
/// finding is classified on its own and its decision never decides the earned
/// base pay of the submission it belongs to.
/// </summary>
public static class FindingRules
{
    /// <summary>
    /// True when a finding may move from one state to another. A confirmed
    /// finding may reopen, because a regression proves that the fix was not a
    /// fix.
    /// </summary>
    public static bool Allows(FindingState from, FindingState to) => from switch
    {
        FindingState.Submitted => to is FindingState.Triaged or FindingState.Confirmed
            or FindingState.Rejected or FindingState.Duplicate,
        FindingState.Triaged => to is FindingState.Confirmed or FindingState.Rejected
            or FindingState.Duplicate,
        FindingState.Confirmed => to is FindingState.Fixing,
        FindingState.Fixing => to is FindingState.Resolved or FindingState.Confirmed,
        FindingState.Resolved => to is FindingState.Confirmed,
        FindingState.Rejected => to is FindingState.Triaged,
        FindingState.Duplicate => false,
        _ => false,
    };
}
