namespace Blindmesh.Domain.Findings;

/// <summary>
/// The severity of a finding. The severity orders the findings of a review; it
/// never decides the earned base pay of a submission.
/// </summary>
public enum FindingSeverity
{
    /// <summary>An observation without a real consequence.</summary>
    Info,

    /// <summary>A small annoyance.</summary>
    Low,

    /// <summary>A defect with a real consequence.</summary>
    Medium,

    /// <summary>A defect that breaks a main flow of the software.</summary>
    High,

    /// <summary>A defect that loses data, leaks a secret or breaks the product.</summary>
    Critical,
}
