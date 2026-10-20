namespace Blindmesh.Domain.Capabilities;

/// <summary>
/// How a capability was verified. A declaration is self declared until a
/// moderator accepts the evidence of a verification. The moderation package is
/// a later package, so this package never moves a capability to
/// <see cref="Verified"/>.
/// </summary>
public enum CapabilityVerification
{
    /// <summary>The owner declared the capability. It is not verified.</summary>
    SelfDeclared,

    /// <summary>A moderator accepted the evidence of the capability.</summary>
    Verified,
}
