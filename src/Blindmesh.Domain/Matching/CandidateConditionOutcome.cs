namespace Blindmesh.Domain.Matching;

/// <summary>
/// The outcome of one hard condition of a task for one candidate. It names the
/// kind and the value of the condition, whether the candidate satisfied it and
/// the public declaration that satisfied it. A withdrawn, a private or an
/// unavailable declaration never reaches this record, so it never holds a
/// private detail of a declaration.
/// </summary>
public sealed record CandidateConditionOutcome(
    string Kind,
    string Value,
    bool Satisfied,
    string? DeclarationLabel,
    string? DeclarationTag);
