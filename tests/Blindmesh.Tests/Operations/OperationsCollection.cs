using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Operations;

/// <summary>
/// The operations tests run against their own isolated PostgreSQL, so the seats
/// of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class OperationsCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "pilot-operations";
}
