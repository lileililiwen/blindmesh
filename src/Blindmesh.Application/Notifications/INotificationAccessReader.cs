namespace Blindmesh.Application.Notifications;

/// <summary>
/// The access reader of the notification module. It answers whether the
/// recipient of a notification may still read the source of it. The answer is
/// read again when the message is sent, so a revoked membership never leaves a
/// private source in the mailbox of the recipient.
/// </summary>
public interface INotificationAccessReader
{
    /// <summary>
    /// True when the recipient may read the source of the notification right
    /// now. A deep link that points at nothing the recipient may read is
    /// false.
    /// </summary>
    Task<bool> MayReadSourceAsync(
        Guid recipientId,
        string deepLink,
        CancellationToken cancellationToken);
}
