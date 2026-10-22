using Blindmesh.Domain.Capabilities;

namespace Blindmesh.Application.Capabilities;

/// <summary>
/// The seam of the capability module for the matching module. The capability
/// module owns the declarations of the contributors and the evaluation of the
/// hard conditions, so it exposes them. The matching module never evaluates a
/// condition itself and never reads a capability table.
/// </summary>
public interface ICandidateMatchReader
{
    /// <summary>
    /// The matchable declarations of every account: the public ones that are
    /// available and not withdrawn. A private, an unavailable or a withdrawn
    /// declaration never reaches a match, so a reason never discloses it.
    /// </summary>
    Task<IReadOnlyList<CandidateDeclaration>> ListCandidatesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// The outcome of every hard condition for one account. The evaluation uses
    /// the public and available declarations of that account only, so the
    /// answer never holds a private detail of a declaration.
    /// </summary>
    Task<IReadOnlyList<CapabilityConditionOutcome>> EvaluateCandidateAsync(
        Guid candidateId,
        IReadOnlyList<CapabilityHardCondition> conditions,
        CancellationToken cancellationToken);
}

/// <summary>
/// One matchable declaration of one account, with the tags of the declaration.
/// The account id names the owner of the declaration; the labels and the tags
/// are the public values a hard condition is matched against.
/// </summary>
public sealed record CandidateDeclaration(
    Guid AccountId,
    Capability Capability,
    IReadOnlyList<string> Tags);
