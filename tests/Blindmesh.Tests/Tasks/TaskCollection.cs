using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// The tasks tests run against their own isolated PostgreSQL, so the tasks of
/// the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class TaskCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "task-publication";
}
