using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// The findings tests run against their own isolated PostgreSQL, so the seats of
/// the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class FindingsCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "evidence-findings";
}
