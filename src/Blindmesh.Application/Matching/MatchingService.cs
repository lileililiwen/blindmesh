using Blindmesh.Application.Capabilities;
using Blindmesh.Application.Reputation;
using Blindmesh.Application.Tasks;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Domain.Matching;
using Blindmesh.Domain.Projects;
using Blindmesh.Domain.Tasks;

namespace Blindmesh.Application.Matching;

/// <summary>
/// The service of the matching module. It owns the match, the reach and the
/// order. The evaluation of the hard conditions stays with the capability
/// module, the conditions of a task stay with the task module and the
/// accepted contributions of an account stay with the reputation module, so
/// this module owns no row and keeps no copy of the state of another module.
///
/// The rule of the scope is applied here and only here: an account that is not
/// an insider of the project of a task reads the task when the project is
/// public and the task is public, or when it satisfies every hard condition
/// of the task and either the task or the project is of the matched scope. An
/// insider reads everything of its project and a private project stays with
/// its insiders.
/// </summary>
public sealed class MatchingService(
    IMatchingStore store,
    ITaskRequirementsReader requirements,
    ICandidateMatchReader candidates,
    IReputationStandingReader reputation) : ITaskMatchSeam
{
    /// <summary>
    /// The accounts that fit a task, with the condition that every declaration
    /// satisfied. Only an insider of the project of the task may read the
    /// answer; every other account is refused without a name and without a
    /// condition.
    /// </summary>
    public async Task<MatchingCommandResult> ListCandidatesAsync(
        Guid actorId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindTaskAsync(taskId, actorId, cancellationToken);

        if (task is null)
        {
            return MatchingCommandResult.NotFound();
        }

        if (!task.IsInsider)
        {
            return MatchingCommandResult.Forbidden();
        }

        var conditions = await requirements.ReadAsync(taskId, cancellationToken) ?? [];

        // A task without a hard condition has no match, so the list of the
        // candidates of such a task is empty.
        if (conditions.Count == 0)
        {
            return MatchingCommandResult.Succeeded(new CandidateListView(
                taskId,
                [],
                []));
        }

        var declarations = await candidates.ListCandidatesAsync(cancellationToken);

        var accountIds = declarations
            .Select(declaration => declaration.AccountId)
            .Distinct()
            .ToArray();

        var accounts = await store.ListAccountsAsync(accountIds, cancellationToken);

        var matches = new List<OrderedCandidate>();

        foreach (var account in accounts)
        {
            // A closed public profile is never part of the answer, so the list
            // never names an account that hides itself.
            if (!account.IsPublicProfile)
            {
                continue;
            }

            var outcomes = await candidates.EvaluateCandidateAsync(
                account.Id,
                conditions,
                cancellationToken);

            if (!outcomes.All(outcome => outcome.Satisfied))
            {
                continue;
            }

            var accepted = await reputation.AcceptedContributionsAsync(
                account.Id,
                cancellationToken);

            matches.Add(new OrderedCandidate(
                new CandidateMatch(
                    account.Id,
                    account.Username,
                    account.DisplayName,
                    ProfileReference(account.Username),
                    accepted,
                    MatchOrderReason.Of(accepted),
                    outcomes
                        .Select(outcome => new CandidateConditionOutcome(
                            outcome.Kind,
                            outcome.Value,
                            outcome.Satisfied,
                            outcome.DeclarationLabel,
                            outcome.DeclarationTag))
                        .ToArray()),
                account.CreatedAt));
        }

        // The rule of the exploration: an account without an accepted
        // contribution comes first, the oldest account of a group comes first
        // and the id breaks the tie, so the order is stable for the same state.
        var ordered = matches
            .OrderBy(candidate => candidate.Match.AcceptedContributions > 0 ? 1 : 0)
            .ThenBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Match.AccountId)
            .Select(candidate => candidate.Match)
            .ToArray();

        return MatchingCommandResult.Succeeded(new CandidateListView(
            taskId,
            conditions
                .Select(condition => new MatchingConditionView(
                    CapabilityService.KindText(condition.Kind),
                    condition.Value.Trim()))
                .ToArray(),
            ordered));
    }

    /// <summary>
    /// The rule of the scope of a task. An insider of the project of the task
    /// reads everything of its project. Every other account reads a public task
    /// of a public and active project, and a task of the matched scope only
    /// when it satisfies every hard condition of the task.
    /// </summary>
    public async Task<bool> MatchesAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var task = await store.FindTaskAsync(taskId, userId, cancellationToken);

        if (task is null)
        {
            return false;
        }

        if (task.IsInsider)
        {
            return true;
        }

        // A private project and a private task stay with the insiders.
        if (task.ProjectVisibility == ProjectVisibility.Private ||
            task.Visibility == TaskVisibility.Private)
        {
            return false;
        }

        // A project that does not accept work does not reach the marketplace.
        if (task.ProjectStatus != ProjectStatus.Active)
        {
            return false;
        }

        if (task.ProjectVisibility == ProjectVisibility.Public &&
            task.Visibility == TaskVisibility.Public)
        {
            return true;
        }

        return await FitsAsync(taskId, userId, cancellationToken);
    }

    /// <summary>
    /// The open tasks of a project whose hard conditions the account
    /// satisfies. The list is empty when the account fits no task, so a
    /// project of the matched scope stays closed for it.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> MatchingTaskIdsAsync(
        Guid projectId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var tasks = await store.ListProjectTasksAsync(projectId, userId, cancellationToken);

        var ids = new List<Guid>();

        foreach (var task in tasks)
        {
            if (await FitsAsync(task.Id, userId, cancellationToken))
            {
                ids.Add(task.Id);
            }
        }

        return ids;
    }

    /// <summary>
    /// True when the account satisfies every hard condition of the task. A
    /// task without a hard condition has no match, so the matched scope never
    /// opens for it.
    /// </summary>
    private async Task<bool> FitsAsync(
        Guid taskId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var conditions = await requirements.ReadAsync(taskId, cancellationToken);

        if (conditions is null || conditions.Count == 0)
        {
            return false;
        }

        var outcomes = await candidates.EvaluateCandidateAsync(
            userId,
            conditions,
            cancellationToken);

        return outcomes.All(outcome => outcome.Satisfied);
    }

    /// <summary>
    /// The reference of the public profile of an account. It is a path of the
    /// product and it never holds a private field of the account.
    /// </summary>
    private static string ProfileReference(string username) => $"/people/{username}";

    /// <summary>One candidate with the moment of the creation of its account.</summary>
    private sealed record OrderedCandidate(CandidateMatch Match, DateTimeOffset CreatedAt);
}
