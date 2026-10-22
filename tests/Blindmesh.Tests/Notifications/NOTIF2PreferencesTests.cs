using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Notifications;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// NOTIF-2 Preferences: the readable state of a notification, the preferences
/// of the recipient and the access of the recipient that is read again when
/// the message goes out.
/// </summary>
[Collection(NotificationCollection.Name)]
public sealed class Notif2PreferencesTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    public Notif2PreferencesTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await NotificationDatabase.ResetAsync(_fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Notif2Success_TheUnreadStateAndThePreferencesPersistWithTheFeedbackOfTheServer()
    {
        await using var sink = new TestMailSink();
        using var host = new NotificationTestHost(_fixture, sink.Port);
        var scenario = new NotificationScenario(host);

        await scenario.RunAcceptedSeatAsync(Guid.NewGuid().ToString("N"));

        await host.WaitForNotificationsAsync(
            rows => rows.Any(row => row.Channel == NotificationChannel.InApp && row.Category == NotificationCategory.Acceptance),
            "the in-app notification of the acceptance");

        await host.SignInAsync("bob");

        using var list = await scenario.ReadNotificationsAsync();
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var item = items.First(entry => entry.GetProperty("category").GetString() == "acceptance");

        Assert.False(item.GetProperty("is_read").GetBoolean());

        var id = item.GetProperty("id").GetString()!;

        // The recipient marks the item as read. The answer of the server holds
        // the committed state.
        var read = await host.SendAsync(HttpMethod.Post, $"/v1/notifications/{id}/read");

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var readBody = await host.ReadJsonAsync(read);

        Assert.True(readBody.RootElement.GetProperty("is_read").GetBoolean());
        Assert.NotNull(readBody.RootElement.GetProperty("read_at").GetString());

        // The unread state persists: the list shows the item as read.
        using var reread = await scenario.ReadNotificationsAsync();
        var rereadItems = reread.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var rereadItem = rereadItems.First(entry => entry.GetProperty("id").GetString() == id);

        Assert.True(rereadItem.GetProperty("is_read").GetBoolean());

        // The recipient changes the optional preference of the email. The
        // answer of the server holds the committed state.
        var preferences = await host.SendAsync(
            HttpMethod.Patch,
            "/v1/notification-preferences",
            "{\"email_enabled\":false}");

        Assert.Equal(HttpStatusCode.OK, preferences.StatusCode);

        using var preferencesBody = await host.ReadJsonAsync(preferences);

        Assert.False(preferencesBody.RootElement.GetProperty("email_enabled").GetBoolean());
        Assert.True(preferencesBody.RootElement.GetProperty("in_app_enabled").GetBoolean());

        // The preferences persist.
        var stored = await host.SendAsync(HttpMethod.Get, "/v1/notification-preferences");

        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);

        using var storedBody = await host.ReadJsonAsync(stored);

        Assert.False(storedBody.RootElement.GetProperty("email_enabled").GetBoolean());
        Assert.True(storedBody.RootElement.GetProperty("in_app_enabled").GetBoolean());
    }

    [Fact]
    public async Task Notif2Rejection_AnotherAccountGetsNoSummaryAndNoActorIdentity()
    {
        await using var sink = new TestMailSink();
        using var host = new NotificationTestHost(_fixture, sink.Port);
        var scenario = new NotificationScenario(host);

        await scenario.RunAcceptedSeatAsync(Guid.NewGuid().ToString("N"));

        await host.WaitForNotificationsAsync(
            rows => rows.Any(row => row.Channel == NotificationChannel.InApp && row.Category == NotificationCategory.Acceptance),
            "the in-app notification of the acceptance");

        await host.SignInAsync("bob");

        using var list = await scenario.ReadNotificationsAsync();
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var item = items.First(entry => entry.GetProperty("category").GetString() == "acceptance");

        var id = item.GetProperty("id").GetString()!;
        var summary = item.GetProperty("summary").GetString()!;

        // Another account reads the list. The item of the first account is not
        // part of it.
        await host.SignInAsync("cara");

        using var other = await scenario.ReadNotificationsAsync();
        var otherItems = other.RootElement.GetProperty("items").EnumerateArray().ToArray();

        Assert.DoesNotContain(otherItems, entry => entry.GetProperty("id").GetString() == id);
        Assert.DoesNotContain(otherItems, entry => entry.GetProperty("summary").GetString() == summary);

        // Another account marks the item as read. The server refuses and the
        // answer holds neither the summary of the item nor the identity of the
        // caller.
        var refused = await host.SendAsync(HttpMethod.Post, $"/v1/notifications/{id}/read");

        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);

        var body = await refused.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain(summary, body, StringComparison.Ordinal);
        Assert.DoesNotContain("bob", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cara", body, StringComparison.OrdinalIgnoreCase);

        // The item of the first account is untouched.
        await host.SignInAsync("bob");

        using var reread = await scenario.ReadNotificationsAsync();
        var rereadItems = reread.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var rereadItem = rereadItems.First(entry => entry.GetProperty("id").GetString() == id);

        Assert.False(rereadItem.GetProperty("is_read").GetBoolean());
    }

    [Fact]
    public async Task Notif2Boundary_AQueuedEmailOfARevokedMembershipHoldsNoPrivateContent()
    {
        await using var sink = new TestMailSink();
        using var host = new NotificationTestHost(_fixture, sink.Port);
        var scenario = new NotificationScenario(host);

        var seat = await scenario.RunAcceptedSeatAsync(Guid.NewGuid().ToString("N"));

        var bobId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "bob")
                .Select(user => user.Id)
                .FirstAsync());

        // The email of the acceptance of the work waits in the queue.
        await host.WaitForNotificationsAsync(
            rows => rows.Any(row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance),
            "the email delivery of the acceptance");

        // The owner of the task closes the project before the message goes out.
        // The contributor may not read the seat any more.
        await host.SignInAsync("alice");

        var version = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking()
                .Where(project => project.Id == Guid.Parse(seat.ProjectId))
                .Select(project => project.Version)
                .FirstAsync());

        var closed = await host.SendAsync(
            HttpMethod.Patch,
            $"/v1/projects/{seat.ProjectId}",
            $"{{\"expected_version\":{version},\"visibility\":\"private\"}}");

        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        // The message goes out after the access was revoked.
        await host.MakeDeliveriesDueAsync();

        var delivery = await host.WaitForDeliveryAsync(
            row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance &&
                row.Status == NotificationStatus.Sent,
            "the sent email of the acceptance");

        Assert.NotEmpty(sink.Accepted);

        var message = sink.Accepted.First(text => text.Contains(delivery.MessageId, StringComparison.Ordinal));

        // The message is a safe generic account notice. It holds no private
        // content and no link to the source.
        Assert.Contains("There is new activity in your account.", message, StringComparison.Ordinal);
        Assert.Contains("/notifications", message, StringComparison.Ordinal);
        Assert.DoesNotContain("/work/", message, StringComparison.Ordinal);
        Assert.DoesNotContain(seat.TaskTitle, message, StringComparison.Ordinal);
        Assert.DoesNotContain("I walked through the seat", message, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", message, StringComparison.OrdinalIgnoreCase);
    }
}
