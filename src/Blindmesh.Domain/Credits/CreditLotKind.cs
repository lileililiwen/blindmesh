namespace Blindmesh.Domain.Credits;

/// <summary>
/// The kind of a credit lot. A grant lot expires, an earned lot never does.
/// </summary>
public enum CreditLotKind
{
    /// <summary>The limited grant of a contributor. It expires after thirty days.</summary>
    Grant,

    /// <summary>Credits that were earned. They never expire.</summary>
    Earned,
}
