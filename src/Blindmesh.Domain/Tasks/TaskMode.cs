namespace Blindmesh.Domain.Tasks;

/// <summary>
/// The modes of a task. A task carries a nonempty subset of them, because one
/// task model covers the whole marketplace: an exploration, a verification, an
/// access to a resource and a review. The modes are stored as a bit set, so the
/// search of the marketplace filters them with the index of the table.
/// </summary>
[Flags]
public enum TaskMode
{
    /// <summary>No mode at all. A task always carries at least one mode.</summary>
    None = 0,

    /// <summary>The contributor explores the software and reports what they find.</summary>
    Explore = 1,

    /// <summary>The contributor verifies a claim against steps and expected results.</summary>
    Verify = 2,

    /// <summary>The contributor reaches a resource of the owner.</summary>
    Access = 4,

    /// <summary>The contributor reviews the work of another contributor.</summary>
    Review = 8,
}
