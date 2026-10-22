using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The reader of the capability module that answers the matching module. It
/// hands the matchable declarations of every account and the outcome of the
/// hard conditions for one account. It is a reader of its own module and not
/// the capability service, so the matching module never depends on the
/// service that reads the conditions of a task: no seam creates a cycle at
/// runtime.
/// </summary>
public sealed class CandidateMatchReader(ICapabilityStore store) : ICandidateMatchReader
{
    public async Task<IReadOnlyList<CandidateDeclaration>> ListCandidatesAsync(
        CancellationToken cancellationToken)
    {
        var declarations = await store.ListAllMatchableAsync(cancellationToken);

        return declarations
            .Select(declaration => new CandidateDeclaration(
                declaration.Capability.OwnerId,
                declaration.Capability,
                declaration.Tags))
            .ToArray();
    }

    public async Task<IReadOnlyList<CapabilityConditionOutcome>> EvaluateCandidateAsync(
        Guid candidateId,
        IReadOnlyList<CapabilityHardCondition> conditions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conditions);

        var declarations = await store.ListMatchableAsync(candidateId, cancellationToken);

        return conditions
            .Select(condition => CapabilityEvaluation.EvaluateForCandidate(condition, declarations))
            .ToArray();
    }
}
