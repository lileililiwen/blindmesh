using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Matching;

/// <summary>
/// The matching tests run against their own isolated PostgreSQL, so the
/// projects, the tasks and the declarations of the other packages never
/// influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class MatchingCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "scoped-matching";
}
