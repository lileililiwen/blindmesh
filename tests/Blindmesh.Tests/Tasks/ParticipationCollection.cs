using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// The participation tests run against their own isolated PostgreSQL, so the
/// seats of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ParticipationCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "participation-execution";
}
