using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The notification tests run against their own isolated PostgreSQL, so the
/// seats and the cases of the other packages never influence the result of a
/// test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class NotificationCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "notification-delivery";
}
