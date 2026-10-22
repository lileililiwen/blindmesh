using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Contracts;
using Blindmesh.Application.Identity;
using Blindmesh.Domain.Notifications;

namespace Blindmesh.Application.Notifications;

/// <summary>
/// The notifications of the recipients. The service owns the queue of the
/// module: every notification is a durable row with its own state, its own
/// attempts and its own schedule, so a failing mailbox never blocks the other
/// deliveries. The module writes no ledger row: a notification never moves
/// credits.
/// </summary>
public sealed class NotificationService(
    INotificationStore store,
    IIdentityStore identity,
    INotificationAccessReader access,
    IMailSender mail,
    IClock clock)
{
    /// <summary>The address the mail channel sends from.</summary>
    public const string MailFromAddress = "no-reply@blindmesh.local";

    /// <summary>The number of attempts one delivery may make.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// The wait before every attempt of a delivery. The first attempt waits a
    /// minute, the next ones wait longer. A delivery that failed its fifth
    /// attempt is dead lettered.
    /// </summary>
    public static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(8),
    ];

    /// <summary>
    /// The time a claimed delivery stays with its worker. A worker that died
    /// without releasing its deliveries hands them back after this time.
    /// </summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    /// <summary>
    /// Writes the notification of one workflow event for one recipient. The
    /// in-app row is written once and the email row is written when the
    /// recipient wants the email. The identity of a delivery is the event, the
    /// recipient and the channel, so a redelivered event never writes a second
    /// row for the same identity.
    /// </summary>
    public async Task<NotificationEnqueueResult> EnqueueAsync(
        string eventType,
        NotificationCategory category,
        Guid recipientId,
        string deepLink,
        CancellationToken cancellationToken)
    {
        var preferences = await ReadPreferencesAsync(recipientId, cancellationToken);
        var created = 0;

        if (preferences.InAppEnabled)
        {
            created += await EnqueueCoreAsync(
                eventType,
                category,
                recipientId,
                deepLink,
                NotificationChannel.InApp,
                null,
                cancellationToken);
        }

        if (preferences.EmailEnabled)
        {
            created += await EnqueueCoreAsync(
                eventType,
                category,
                recipientId,
                deepLink,
                NotificationChannel.Email,
                null,
                cancellationToken);
        }

        return new NotificationEnqueueResult(created);
    }

    /// <summary>
    /// The notifications of the signed in recipient, the newest first. Only the
    /// rows of the recipient are read, so a notification never leaks into the
    /// list of another account.
    /// </summary>
    public async Task<PagedResult<NotificationView>> ListAsync(
        Guid actorId,
        NotificationCursor? cursor,
        int? limit,
        CancellationToken cancellationToken)
    {
        var size = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var rows = await store.ListDeliveriesAsync(actorId, cursor, size + 1, cancellationToken);
        var page = rows.Take(size).ToArray();
        var views = page.Select(BuildView).ToArray();
        string? nextCursor = null;

        if (rows.Count > size && page.Length > 0)
        {
            nextCursor = new NotificationCursor(page[^1].CreatedAt, page[^1].Id).Encode();
        }

        return new PagedResult<NotificationView>
        {
            Items = views,
            NextCursor = nextCursor,
        };
    }

    /// <summary>
    /// Marks one notification of the signed in recipient as read. A
    /// notification of another account is not found, and the answer carries
    /// neither the summary of the notification nor the identity of the caller.
    /// </summary>
    public async Task<NotificationReadResult> MarkReadAsync(
        Guid actorId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var delivery = await store.FindDeliveryAsync(notificationId, cancellationToken);

        if (delivery is null ||
            delivery.RecipientId != actorId ||
            delivery.Channel != NotificationChannel.InApp)
        {
            return new NotificationReadResult(NotificationReadStatus.NotFound, null);
        }

        if (delivery.ReadAt is not null)
        {
            return new NotificationReadResult(NotificationReadStatus.AlreadyRead, BuildView(delivery));
        }

        delivery.ReadAt = clock.UtcNow;
        await store.SaveChangesAsync(cancellationToken);

        return new NotificationReadResult(NotificationReadStatus.Read, BuildView(delivery));
    }

    /// <summary>The preferences of the signed in recipient.</summary>
    public async Task<NotificationPreferenceView> GetPreferencesAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        var preference = await store.FindPreferenceAsync(actorId, cancellationToken);

        return new NotificationPreferenceView(
            preference?.InAppEnabled ?? true,
            preference?.EmailEnabled ?? true,
            preference?.UpdatedAt);
    }

    /// <summary>
    /// Saves the preferences of the signed in recipient. The answer holds the
    /// committed state, so the interface announces the saved state only after
    /// the server confirmed it.
    /// </summary>
    public async Task<NotificationPreferenceResult> UpdatePreferencesAsync(
        Guid actorId,
        NotificationPreferenceInput input,
        CancellationToken cancellationToken)
    {
        var stored = await store.FindPreferenceAsync(actorId, cancellationToken);
        var preference = stored ?? new NotificationPreference { UserId = actorId };
        var inApp = input.InAppEnabled ?? preference.InAppEnabled;
        var email = input.EmailEnabled ?? preference.EmailEnabled;

        if (stored is not null && stored.InAppEnabled == inApp && stored.EmailEnabled == email)
        {
            return new NotificationPreferenceResult(
                NotificationPreferenceStatus.Unchanged,
                new NotificationPreferenceView(inApp, email, stored.UpdatedAt));
        }

        preference.InAppEnabled = inApp;
        preference.EmailEnabled = email;
        preference.UpdatedAt = clock.UtcNow;

        if (stored is null)
        {
            await store.AddPreferenceAsync(preference, cancellationToken);
        }

        await store.SaveChangesAsync(cancellationToken);

        return new NotificationPreferenceResult(
            NotificationPreferenceStatus.Updated,
            new NotificationPreferenceView(inApp, email, preference.UpdatedAt));
    }

    /// <summary>
    /// Claims the deliveries that may be sent now. The rows are locked with FOR
    /// UPDATE SKIP LOCKED, so two workers never claim the same delivery.
    /// </summary>
    public async Task<IReadOnlyList<NotificationDelivery>> ClaimDueAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var claimed = await store.ClaimDueAsync(now, Math.Clamp(limit, 1, MaxPageSize), cancellationToken);

        foreach (var delivery in claimed)
        {
            delivery.AttemptCount += 1;
            delivery.Status = NotificationStatus.Sending;
            delivery.NextAttemptAt = now + ClaimLease;
        }

        await store.SaveChangesAsync(cancellationToken);

        return claimed;
    }

    /// <summary>
    /// Sends one claimed delivery. The in-app channel is the durable row
    /// itself, so it never fails. The email channel rechecks the access of the
    /// recipient before it sends: a recipient that may not read the source any
    /// more only receives a safe generic account notice.
    /// </summary>
    public async Task<bool> DeliverAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (delivery.Channel == NotificationChannel.InApp)
        {
            await store.MarkSentAsync(delivery, clock.UtcNow, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            return true;
        }

        var recipient = await identity.FindUserAsync(delivery.RecipientId, cancellationToken);

        if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
        {
            await HandleFailureAsync(delivery, "the recipient has no mailbox", cancellationToken);

            return false;
        }

        var allowed = await access.MayReadSourceAsync(
            delivery.RecipientId,
            delivery.DeepLink,
            cancellationToken);

        var message = new MailMessage(
            MailFromAddress,
            recipient.Email,
            delivery.Subject,
            allowed ? BuildBody(delivery) : BuildGenericBody(),
            delivery.MessageId);

        var result = await mail.SendAsync(message, cancellationToken);

        if (result.Sent)
        {
            await store.MarkSentAsync(delivery, clock.UtcNow, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);

            return true;
        }

        await HandleFailureAsync(delivery, result.Error, cancellationToken);

        return false;
    }

    /// <summary>
    /// The safe summary of a category. The text never holds a title, a finding
    /// text, a party, an amount or a link.
    /// </summary>
    public static string SummaryText(NotificationCategory category) => category switch
    {
        NotificationCategory.ApplicationReceived =>
            "A task received an application for one of its seats.",
        NotificationCategory.ApplicationAccepted =>
            "A task accepted your application for one of its seats.",
        NotificationCategory.Deadline =>
            "A deadline of your work is near.",
        NotificationCategory.SubmissionReceived =>
            "The owner of a task received new evidence of a seat.",
        NotificationCategory.CorrectionRequested =>
            "The owner of a task asked for a correction of your work.",
        NotificationCategory.Acceptance =>
            "Your work was accepted.",
        NotificationCategory.Refund =>
            "Credits were returned to your wallet.",
        NotificationCategory.DisputeUpdate =>
            "A case of arbitration changed.",
        NotificationCategory.ModerationOutcome =>
            "A decision about the work changed its state.",
        _ => "There is new activity in your account.",
    };

    /// <summary>The short label of a category.</summary>
    public static string LabelText(NotificationCategory category) => category switch
    {
        NotificationCategory.ApplicationReceived => "Application received",
        NotificationCategory.ApplicationAccepted => "Application accepted",
        NotificationCategory.Deadline => "Deadline",
        NotificationCategory.SubmissionReceived => "New evidence",
        NotificationCategory.CorrectionRequested => "Correction requested",
        NotificationCategory.Acceptance => "Work accepted",
        NotificationCategory.Refund => "Refund",
        NotificationCategory.DisputeUpdate => "Case update",
        NotificationCategory.ModerationOutcome => "Decision",
        _ => "Activity",
    };

    /// <summary>The safe subject of a category.</summary>
    public static string SubjectText(NotificationCategory category) =>
        $"Blindmesh: {LabelText(category)}";

    /// <summary>
    /// The safe deep link of a notification. Only a relative path inside the
    /// product is kept; an absolute address, a query, a fragment or a value
    /// that is too long falls back to the page of the notifications.
    /// </summary>
    public static string SafeDeepLink(string? deepLink)
    {
        if (string.IsNullOrWhiteSpace(deepLink))
        {
            return "/notifications";
        }

        var trimmed = deepLink.Trim();

        if (!trimmed.StartsWith('/') ||
            trimmed.Contains("://", StringComparison.Ordinal) ||
            trimmed.Contains('?', StringComparison.Ordinal) ||
            trimmed.Contains('#', StringComparison.Ordinal) ||
            trimmed.Length > 200)
        {
            return "/notifications";
        }

        return trimmed;
    }

    /// <summary>The stable message identifier of a delivery.</summary>
    public static string BuildMessageId(Guid deliveryId) => $"<{deliveryId:D}@blindmesh.local>";

    /// <summary>The identity of a delivery: the event, the recipient and the channel.</summary>
    public static string BuildUniqueKey(
        string eventType,
        Guid recipientId,
        NotificationChannel channel) =>
        $"{eventType}|{recipientId:D}|{NotificationDelivery.ChannelText(channel)}";

    private async Task<int> EnqueueCoreAsync(
        string eventType,
        NotificationCategory category,
        Guid recipientId,
        string deepLink,
        NotificationChannel channel,
        string? summary,
        CancellationToken cancellationToken)
    {
        var uniqueKey = BuildUniqueKey(eventType, recipientId, channel);
        var known = await store.FindByUniqueKeyAsync(uniqueKey, cancellationToken);

        if (known is not null)
        {
            // The same event arrived twice. The identity of the delivery
            // already exists, so nothing is written.
            return 0;
        }

        var id = Guid.NewGuid();
        var now = clock.UtcNow;
        var delivery = new NotificationDelivery
        {
            Id = id,
            RecipientId = recipientId,
            EventType = eventType,
            Category = category,
            Channel = channel,
            UniqueKey = uniqueKey,
            Subject = SubjectText(category),
            Summary = summary ?? SummaryText(category),
            DeepLink = SafeDeepLink(deepLink),
            Status = NotificationStatus.Pending,
            AttemptCount = 0,
            NextAttemptAt = channel == NotificationChannel.Email ? now + RetryDelays[0] : now,
            MessageId = BuildMessageId(id),
            CreatedAt = now,
        };

        await store.AddDeliveryAsync(delivery, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);

        return 1;
    }

    private async Task HandleFailureAsync(
        NotificationDelivery delivery,
        string? error,
        CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;

        if (delivery.AttemptCount >= MaxAttempts)
        {
            await store.MarkDeadLetteredAsync(delivery, now, Redact(error), cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            await WriteOperatorAlertAsync(delivery, cancellationToken);

            return;
        }

        var next = now + RetryDelays[delivery.AttemptCount];

        await store.MarkFailedAsync(delivery, now, next, Redact(error), cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Writes the alert of the operator. The alert is an in-app notification for
    /// every account that administers the product, so the operator sees it in
    /// the product and not only in the log.
    /// </summary>
    private async Task WriteOperatorAlertAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        var administrators = await store.ListAdministratorIdsAsync(cancellationToken);
        var eventType = NotificationEventTypes.Identity(NotificationEventTypes.OperatorAlert, delivery.Id);
        const string summary =
            "A notification delivery failed every attempt. The operator has to look at the queue.";

        foreach (var administratorId in administrators)
        {
            await EnqueueCoreAsync(
                eventType,
                NotificationCategory.ModerationOutcome,
                administratorId,
                "/notifications",
                NotificationChannel.InApp,
                summary,
                cancellationToken);
        }
    }

    /// <summary>
    /// The body of a notification that the recipient may read. It holds the
    /// generic summary and the relative deep link, nothing else.
    /// </summary>
    private static string BuildBody(NotificationDelivery delivery) =>
        $"{delivery.Summary}{Environment.NewLine}{Environment.NewLine}{delivery.DeepLink}";

    /// <summary>
    /// The body of a notification whose source the recipient may not read any
    /// more. It holds no private content and no link to the source.
    /// </summary>
    private static string BuildGenericBody() =>
        $"There is new activity in your account.{Environment.NewLine}{Environment.NewLine}/notifications";

    /// <summary>
    /// The safe reason of a failed attempt. It never holds a provider detail, a
    /// host, a port, a credential or a stack trace.
    /// </summary>
    private static string? Redact(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "the attempt failed";
        }

        var trimmed = error.Trim();

        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }

    private async Task<NotificationPreferenceView> ReadPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var preference = await store.FindPreferenceAsync(userId, cancellationToken);

        return new NotificationPreferenceView(
            preference?.InAppEnabled ?? true,
            preference?.EmailEnabled ?? true,
            preference?.UpdatedAt);
    }

    private static NotificationView BuildView(NotificationDelivery delivery) =>
        new(
            delivery.Id,
            NotificationDelivery.CategoryText(delivery.Category),
            LabelText(delivery.Category),
            delivery.Summary,
            delivery.DeepLink,
            delivery.ReadAt is not null,
            delivery.CreatedAt,
            delivery.ReadAt);
}
