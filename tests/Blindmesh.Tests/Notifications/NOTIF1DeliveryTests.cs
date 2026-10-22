using System.Net;
using System.Text.Json;
using Blindmesh.Application.Notifications;
using Blindmesh.Domain.Notifications;
using Blindmesh.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// NOTIF-1 Delivery: the durable in-app notification and the retryable email
/// of a workflow event. The outbox consumer of the product and the sender of
/// the product run inside the host of the test, so a notification of the test
/// is written and sent by the same code that runs in production.
/// </summary>
[Collection(NotificationCollection.Name)]
public sealed class Notif1DeliveryTests : IAsyncLifetime
{
    private readonly RuntimeFixture _fixture;

    public Notif1DeliveryTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await NotificationDatabase.ResetAsync(_fixture);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Notif1Success_OneInAppItemAppearsAndAnEligibleEmailIsSentWithAStableMessageId()
    {
        var reader = new MailpitReader();

        Assert.True(
            await reader.IsReachableAsync(CancellationToken.None),
            "The mail sink of the test run does not answer. The real transport of the mail channel cannot be proved without it.");

        using var host = new NotificationTestHost(_fixture, MailpitReader.SmtpPort);
        var scenario = new NotificationScenario(host);
        var stamp = Guid.NewGuid().ToString("N");

        var seat = await scenario.RunAcceptedSeatAsync(stamp);

        var bobId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "bob")
                .Select(user => user.Id)
                .FirstAsync());

        // The consumer of the outbox writes the notification of the contributor.
        await host.WaitForNotificationsAsync(
            rows => rows.Any(row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.InApp &&
                row.Category == NotificationCategory.Acceptance),
            "the in-app notification of the acceptance");

        // The in-app item appears in the list of the contributor, and it holds
        // nothing private.
        await host.SignInAsync("bob");

        using var list = await scenario.ReadNotificationsAsync();
        var items = list.RootElement.GetProperty("items").EnumerateArray().ToArray();
        var acceptance = items
            .Where(item => item.GetProperty("category").GetString() == "acceptance")
            .ToArray();

        Assert.Single(acceptance);
        Assert.False(acceptance[0].GetProperty("is_read").GetBoolean());

        var summary = acceptance[0].GetProperty("summary").GetString()!;

        Assert.DoesNotContain(seat.TaskTitle, summary, StringComparison.Ordinal);
        Assert.DoesNotContain("blindmesh", summary, StringComparison.OrdinalIgnoreCase);

        // The email of the same event goes out through the real transport of
        // the mail channel.
        await host.MakeDeliveriesDueAsync();

        var delivery = await host.WaitForDeliveryAsync(
            row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance &&
                row.Status == NotificationStatus.Sent,
            "the sent email of the acceptance");

        Assert.Equal(NotificationService.BuildMessageId(delivery.Id), delivery.MessageId);
        Assert.NotNull(delivery.SentAt);

        var message = await reader.FindByMessageIdAsync(delivery.MessageId, CancellationToken.None);

        Assert.NotNull(message);

        // The identifier of the delivery is the identifier of the message. The
        // sink strips the angle brackets of the header, so both sides are
        // compared in the same shape.
        Assert.Equal(
            delivery.MessageId.Trim('<', '>'),
            message!.MessageId.Trim('<', '>'),
            StringComparer.Ordinal);
        Assert.Contains("bob@blindmesh.test", message.To);
        Assert.Equal(NotificationService.SubjectText(NotificationCategory.Acceptance), message.Subject);

        // The body holds the generic summary and the relative deep link, and
        // nothing private.
        Assert.Contains(summary, message.Text, StringComparison.Ordinal);
        Assert.Contains("/work/", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(seat.TaskTitle, message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("I walked through the seat", message.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", message.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", message.Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Notif1Rejection_AFailedAttemptKeepsItsRetryStateAndStatesTheUncertainty()
    {
        // The sink accepts the connection and never answers, so the attempt of
        // the mail channel runs into its timeout and the outcome of the
        // attempt stays uncertain.
        await using var sink = new TestMailSink(silent: true);
        using var host = new NotificationTestHost(_fixture, sink.Port);
        var logger = host.Logger;

        var scenario = new NotificationScenario(host);
        await scenario.RunAcceptedSeatAsync(Guid.NewGuid().ToString("N"));

        var bobId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "bob")
                .Select(user => user.Id)
                .FirstAsync());

        await host.WaitForNotificationsAsync(
            rows => rows.Any(row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance),
            "the email delivery of the acceptance");

        await host.MakeDeliveriesDueAsync();

        var delivery = await host.WaitForDeliveryAsync(
            row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance &&
                row.Status == NotificationStatus.Failed,
            "the failed attempt of the mail channel");

        // The retry state persists: the attempt is counted, the reason is
        // stored and the next attempt lies in the future.
        Assert.Equal(1, delivery.AttemptCount);
        Assert.NotNull(delivery.LastError);
        Assert.True(
            delivery.NextAttemptAt > DateTimeOffset.UtcNow,
            "The next attempt of a failed delivery must lie in the future.");

        // The stored reason is redacted: it holds no host, no port and no
        // credential.
        Assert.DoesNotContain("127.0.0.1", delivery.LastError!, StringComparison.Ordinal);
        Assert.DoesNotContain(sink.Port.ToString(), delivery.LastError!, StringComparison.Ordinal);
        Assert.DoesNotContain("blindmesh.test", delivery.LastError!, StringComparison.OrdinalIgnoreCase);

        // The identifier of the message is stable, so a retry is recognisable
        // and the email is never promised exactly once.
        Assert.Equal(NotificationService.BuildMessageId(delivery.Id), delivery.MessageId);

        // The log states the uncertainty of the attempt.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        var line = string.Empty;

        while (DateTimeOffset.UtcNow < deadline && line.Length == 0)
        {
            line = logger.Lines.FirstOrDefault(text => text.Contains(delivery.Id.ToString(), StringComparison.Ordinal)) ?? string.Empty;

            if (line.Length == 0)
            {
                await Task.Delay(200);
            }
        }

        Assert.NotEmpty(line);
        Assert.Contains("more than once", line, StringComparison.Ordinal);
        Assert.DoesNotContain("exactly once", line, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Notif1Boundary_TheDeliveryIsDeadLetteredWithARedactedOperatorAlertWhileOtherDeliveriesContinue()
    {
        // The sink refuses the recipient of the delivery that dies, so the
        // attempt fails at once. The other deliveries go out.
        await using var sink = new TestMailSink(refusedRecipient: "bob@blindmesh.test");
        using var host = new NotificationTestHost(_fixture, sink.Port);
        var logger = host.Logger;

        var scenario = new NotificationScenario(host);
        await scenario.RunAcceptedSeatAsync(Guid.NewGuid().ToString("N"));

        // The account of the operator of the product signs in, so the alert of
        // the dead letter has a recipient.
        await host.SignInAsync("erin");

        var bobId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "bob")
                .Select(user => user.Id)
                .FirstAsync());

        var erinId = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Users.AsNoTracking()
                .Where(user => user.Username == "erin")
                .Select(user => user.Id)
                .FirstAsync());

        await host.WaitForNotificationsAsync(
            rows => rows.Any(row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance),
            "the email delivery of the acceptance");

        // The wait of the first attempt of a delivery is the first delay of the
        // schedule of the product.
        var created = await host.WaitForDeliveryAsync(
            row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance &&
                row.Status == NotificationStatus.Pending,
            "the email delivery of the acceptance");

        Assert.Equal(
            NotificationService.RetryDelays[0],
            created.NextAttemptAt - created.CreatedAt);

        // The worker walks the whole ladder of the delays and the delivery dies
        // after its last attempt.
        for (var attempt = 1; attempt <= NotificationService.MaxAttempts; attempt++)
        {
            await host.MakeDeliveriesDueAsync();

            var failed = await host.WaitForDeliveryAsync(
                row =>
                    row.RecipientId == bobId &&
                    row.Channel == NotificationChannel.Email &&
                    row.Category == NotificationCategory.Acceptance &&
                    row.Status != NotificationStatus.Pending &&
                    row.Status != NotificationStatus.Sending &&
                    row.AttemptCount >= attempt,
                $"the attempt {attempt} of the mail channel");

            if (attempt < NotificationService.MaxAttempts)
            {
                Assert.Equal(NotificationStatus.Failed, failed.Status);

                // The wait of the next attempt is the next delay of the
                // schedule of the product.
                var expected = NotificationService.RetryDelays[attempt];

                Assert.True(
                    failed.NextAttemptAt >= failed.CreatedAt.AddDays(-1) &&
                    failed.NextAttemptAt - DateTimeOffset.UtcNow <= expected &&
                    failed.NextAttemptAt - DateTimeOffset.UtcNow > expected - TimeSpan.FromMinutes(1),
                    $"The wait of the attempt {attempt + 1} must be the delay of {expected}.");
            }
        }

        var dead = await host.WaitForDeliveryAsync(
            row =>
                row.RecipientId == bobId &&
                row.Channel == NotificationChannel.Email &&
                row.Category == NotificationCategory.Acceptance &&
                row.Status == NotificationStatus.DeadLettered,
            "the dead letter of the delivery");

        Assert.NotNull(dead.DeadLetteredAt);
        Assert.NotNull(dead.LastError);
        Assert.DoesNotContain("127.0.0.1", dead.LastError!, StringComparison.Ordinal);
        Assert.DoesNotContain(sink.Port.ToString(), dead.LastError!, StringComparison.Ordinal);

        // The operator is told about the delivery in the product itself.
        await host.WaitForNotificationsAsync(
            rows => rows.Any(row =>
                row.RecipientId == erinId &&
                row.Channel == NotificationChannel.InApp &&
                row.EventType.Contains(NotificationEventTypes.OperatorAlert, StringComparison.Ordinal)),
            "the operator alert of the dead letter");

        // The log names the dead letter and holds no provider detail.
        var line = logger.Lines.FirstOrDefault(text =>
            text.Contains(dead.Id.ToString(), StringComparison.Ordinal) &&
            text.Contains("dead lettered", StringComparison.Ordinal));

        Assert.NotNull(line);
        Assert.DoesNotContain("127.0.0.1", line!, StringComparison.Ordinal);
        Assert.DoesNotContain(sink.Port.ToString(), line!, StringComparison.Ordinal);

        // The other deliveries continue: the email of the owner of the task
        // went out, although the delivery of the contributor died.
        var owner = await host.WaitForDeliveryAsync(
            row =>
                row.Channel == NotificationChannel.Email &&
                row.RecipientId != bobId &&
                row.Status == NotificationStatus.Sent,
            "the sent email of the owner of the task");

        Assert.NotNull(owner.SentAt);
    }
}
