using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// The credits tests run against their own isolated PostgreSQL, so the credits
/// of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class CreditCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "credit-ledger";
}
