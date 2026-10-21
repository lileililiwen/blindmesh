using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// The moderation tests run against their own isolated PostgreSQL, so the seats
/// and the cases of the other packages never influence the result of a test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ModerationCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "moderation-arbitration";
}
