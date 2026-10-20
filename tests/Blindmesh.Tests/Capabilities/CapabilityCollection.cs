using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Capabilities;

/// <summary>
/// The capabilities tests run against their own isolated PostgreSQL, so the
/// capabilities of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CapabilityCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "capability-directory";
}
