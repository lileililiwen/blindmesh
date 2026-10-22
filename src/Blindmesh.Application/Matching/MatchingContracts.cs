using Blindmesh.Domain.Matching;

namespace Blindmesh.Application.Matching;

/// <summary>The outcome of a command of the matching module.</summary>
public enum MatchingCommandStatus
{
    /// <summary>The command answered.</summary>
    Succeeded,

    /// <summary>The task does not exist or may not be seen by the actor.</summary>
    NotFound,

    /// <summary>The actor may not read the answer of the matching module.</summary>
    Forbidden,
}

/// <summary>
/// The result of a command of the matching module. The message of a refusal
/// names neither a candidate nor a condition, so a refused read teaches
/// nothing about the task.
/// </summary>
public sealed record MatchingCommandResult(
    MatchingCommandStatus Status,
    CandidateListView? List = null,
    string? Message = null)
{
    /// <summary>The answer of the matching module.</summary>
    public static MatchingCommandResult Succeeded(CandidateListView list) =>
        new(MatchingCommandStatus.Succeeded, list);

    /// <summary>The task is not found for the actor.</summary>
    public static MatchingCommandResult NotFound() =>
        new(
            MatchingCommandStatus.NotFound,
            Message: "The task does not exist or is not visible to you.");

    /// <summary>The actor may not read the candidates of the task.</summary>
    public static MatchingCommandResult Forbidden() =>
        new(
            MatchingCommandStatus.Forbidden,
            Message: "Only the members of the project of the task may read its candidates.");
}

/// <summary>
/// The list of the accounts that fit a task, with the conditions of the task.
/// The list carries no score, no weight, no percentage and no rank.
/// </summary>
public sealed record CandidateListView(
    Guid TaskId,
    IReadOnlyList<MatchingConditionView> Conditions,
    IReadOnlyList<CandidateMatch> Candidates);

/// <summary>One hard condition of a task, in the vocabulary of the wire.</summary>
public sealed record MatchingConditionView(string Kind, string Value);
