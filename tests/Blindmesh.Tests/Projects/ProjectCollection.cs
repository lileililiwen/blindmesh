using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Projects;

/// <summary>
/// The projects tests run against their own isolated PostgreSQL, so the
/// projects of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ProjectCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "project-workspaces";
}
