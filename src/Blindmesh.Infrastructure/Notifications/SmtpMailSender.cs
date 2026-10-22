using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Blindmesh.Application.Configuration;
using Blindmesh.Application.Notifications;
using Microsoft.Extensions.Options;

namespace Blindmesh.Infrastructure.Notifications;

/// <summary>
/// The mail channel of the product. It is a small SMTP client over a socket:
/// the product sends one message per attempt and never opens a socket in the
/// application layer. The transport failures are answered as a safe reason, so
/// the log of a failure never holds a host, a port, a provider detail or a
/// credential.
/// </summary>
public sealed class SmtpMailSender(IOptions<BlindmeshRuntimeOptions> options) : IMailSender
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private const string ClientName = "blindmesh";

    public async Task<MailSendResult> SendAsync(MailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = options.Value.Mail;

        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            return MailSendResult.Failed("the mail service is not configured");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(settings.Host, settings.Port, timeout.Token);

            await using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
            {
                AutoFlush = true,
                NewLine = "\r\n",
            };

            var greeting = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(greeting) != 220)
            {
                return MailSendResult.Failed("the mail service refused the connection");
            }

            await WriteAsync(writer, $"EHLO {ClientName}", timeout.Token);
            var hello = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(hello) != 250)
            {
                return MailSendResult.Failed("the mail service refused the greeting");
            }

            await WriteAsync(writer, $"MAIL FROM:<{message.From}>", timeout.Token);
            var sender = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(sender) != 250)
            {
                return MailSendResult.Failed("the mail service refused the sender");
            }

            await WriteAsync(writer, $"RCPT TO:<{message.To}>", timeout.Token);
            var recipient = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(recipient) is not (250 or 251))
            {
                return MailSendResult.Failed("the mail service refused the recipient");
            }

            await WriteAsync(writer, "DATA", timeout.Token);
            var data = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(data) != 354)
            {
                return MailSendResult.Failed("the mail service refused the message");
            }

            await WriteAsync(writer, BuildMessage(message), timeout.Token);
            var accepted = await ReadResponseAsync(reader, timeout.Token);

            if (ResponseCode(accepted) != 250)
            {
                return MailSendResult.Failed("the mail service did not accept the message");
            }

            await WriteAsync(writer, "QUIT", timeout.Token);
            await ReadResponseAsync(reader, timeout.Token);

            return MailSendResult.Ok();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return MailSendResult.Failed("the mail service did not answer in time");
        }
        catch (SocketException)
        {
            return MailSendResult.Failed("the mail service could not be reached");
        }
        catch (IOException)
        {
            return MailSendResult.Failed("the connection to the mail service broke");
        }
    }

    /// <summary>
    /// The message of the mail channel. It holds the safe subject, the safe
    /// body and the stable message identifier of the delivery. It never holds
    /// an attachment.
    /// </summary>
    private static string BuildMessage(MailMessage message)
    {
        var builder = new StringBuilder();

        builder.Append("From: ").Append(message.From).Append("\r\n");
        builder.Append("To: ").Append(message.To).Append("\r\n");
        builder.Append("Subject: ").Append(message.Subject).Append("\r\n");
        builder.Append("Message-ID: ").Append(message.MessageId).Append("\r\n");
        builder.Append("Date: ")
            .Append(DateTimeOffset.UtcNow.ToString("r", CultureInfo.InvariantCulture))
            .Append("\r\n");
        builder.Append("MIME-Version: 1.0\r\n");
        builder.Append("Content-Type: text/plain; charset=utf-8\r\n");
        builder.Append("Content-Transfer-Encoding: 8bit\r\n");
        builder.Append("\r\n");

        foreach (var line in NormaliseLines(message.Body).Split('\n'))
        {
            // A line that starts with a dot ends the message for the server,
            // so it is escaped.
            builder.Append(line.StartsWith('.') ? "." + line : line).Append("\r\n");
        }

        builder.Append(".\r\n");

        return builder.ToString();
    }

    /// <summary>The body of the message with the line endings of the protocol.</summary>
    private static string NormaliseLines(string body) =>
        body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    /// <summary>
    /// Reads one answer of the server. The answer may span several lines; the
    /// last line of it carries a space after the code.
    /// </summary>
    private static async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (line is null)
            {
                throw new IOException("the mail service closed the connection");
            }

            builder.Append(line);

            if (line.Length >= 4 && line[3] == '-')
            {
                // The answer continues in the next line.
                builder.Append('\n');
                continue;
            }

            break;
        }

        return builder.ToString();
    }

    /// <summary>The code of the answer of the server.</summary>
    private static int ResponseCode(string response)
    {
        if (response.Length < 3 ||
            !int.TryParse(response.AsSpan(0, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var code))
        {
            return 0;
        }

        return code;
    }

    private static async Task WriteAsync(StreamWriter writer, string line, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(line.AsMemory(), cancellationToken);
        await writer.WriteAsync(writer.NewLine.AsMemory(), cancellationToken);
    }
}
