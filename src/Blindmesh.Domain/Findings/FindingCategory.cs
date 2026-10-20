namespace Blindmesh.Domain.Findings;

/// <summary>
/// The category of a finding. The category names the kind of the defect and
/// never carries a credential of a resource.
/// </summary>
public enum FindingCategory
{
    /// <summary>The software does not do what it promises.</summary>
    Bug,

    /// <summary>The flow of the software is hard to use or hard to understand.</summary>
    Ux,

    /// <summary>The software is slower than it should be.</summary>
    Performance,

    /// <summary>The software leaks or exposes something it should not.</summary>
    Security,

    /// <summary>The software is hard to use with a keyboard or with a screen reader.</summary>
    Accessibility,

    /// <summary>The software does not work on a supported platform or a browser.</summary>
    Compatibility,

    /// <summary>The documentation of the software is wrong or missing.</summary>
    Documentation,
}
