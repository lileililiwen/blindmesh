namespace Blindmesh.Domain.Matching;

/// <summary>
/// One account that fits a task. The record holds the public identity of the
/// account, the number of its accepted contributions, the reason of its place
/// in the order and the outcome of every hard condition of the task. It never
/// holds a score, a weight, a percentage, a rank, a quality claim, the email
/// of an account or the biography of an account.
/// </summary>
public sealed record CandidateMatch(
    Guid AccountId,
    string Username,
    string DisplayName,
    string ProfileReference,
    int AcceptedContributions,
    MatchOrderReason OrderReason,
    IReadOnlyList<CandidateConditionOutcome> Conditions);
