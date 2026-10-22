namespace Blindmesh.Application.Notifications;

/// <summary>
/// One message of the mail channel. It carries the safe subject, the safe body
/// and the stable message identifier of the delivery. It never carries an
/// attachment, a credential or a private title.
/// </summary>
public sealed record MailMessage(
    string From,
    string To,
    string Subject,
    string Body,
    string MessageId);

/// <summary>
/// The outcome of one attempt of the mail channel. A failure never carries a
/// provider detail, a host, a port or a credential: only a short safe reason
/// that names the kind of the failure.
/// </summary>
public sealed record MailSendResult(bool Sent, string? Error)
{
    /// <summary>The message went out.</summary>
    public static MailSendResult Ok() => new(true, null);

    /// <summary>The message did not go out. The reason is safe to log.</summary>
    public static MailSendResult Failed(string reason) => new(false, reason);
}

/// <summary>
/// The mail channel of the product. The application layer never opens a socket:
/// the implementation of the sender lives in the infrastructure.
/// </summary>
public interface IMailSender
{
    /// <summary>
    /// Sends one message. The sender never throws for a documented transport
    /// outcome: it answers with the outcome of the attempt.
    /// </summary>
    Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken);
}
