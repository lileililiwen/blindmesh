using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// A local SMTP sink for the tests of the delivery of a notification. It
/// speaks the small part of the protocol that the mail channel of the product
/// uses, so a failure of the transport is a real failure of the transport and
/// not a stand in of the answer.
/// </summary>
internal sealed class TestMailSink : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly string? _refusedRecipient;
    private readonly bool _silent;
    private readonly List<string> _accepted = [];

    /// <summary>
    /// The sink. When a refused recipient is named, the sink answers the
    /// recipient with a refusal of the mail service. When the sink is silent,
    /// it accepts the connection and never answers, so the sender runs into
    /// its timeout and the outcome of the attempt stays uncertain.
    /// </summary>
    public TestMailSink(string? refusedRecipient = null, bool silent = false)
    {
        _refusedRecipient = refusedRecipient;
        _silent = silent;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }

    public int Port { get; }

    /// <summary>The messages the sink accepted, in the order they arrived.</summary>
    public IReadOnlyList<string> Accepted
    {
        get
        {
            lock (_accepted)
            {
                return _accepted.ToArray();
            }
        }
    }

    private async Task AcceptAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_shutdown.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            _ = RespondAsync(client);
        }
    }

    private async Task RespondAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();

                if (_silent)
                {
                    // The connection stays open and the sink never answers, so
                    // the sender runs into its timeout.
                    await Task.Delay(Timeout.InfiniteTimeSpan, _shutdown.Token);
                    return;
                }

                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true)
                {
                    AutoFlush = true,
                    NewLine = "\r\n",
                };

                await writer.WriteAsync("220 blindmesh test sink\r\n");

                while (true)
                {
                    var line = await reader.ReadLineAsync();

                    if (line is null)
                    {
                        return;
                    }

                    var command = line.Split(' ', 2)[0].ToUpperInvariant();

                    switch (command)
                    {
                        case "EHLO":
                            await writer.WriteAsync("250-blindmesh test sink\r\n250 OK\r\n");
                            break;

                        case "HELO":
                            await writer.WriteAsync("250 blindmesh test sink\r\n");
                            break;

                        case "MAIL":
                            await writer.WriteAsync("250 OK\r\n");
                            break;

                        case "RCPT":
                            var refused = _refusedRecipient is not null &&
                                line.Contains(_refusedRecipient, StringComparison.OrdinalIgnoreCase);
                            await writer.WriteAsync(refused ? "550 the mailbox is not available\r\n" : "250 OK\r\n");
                            break;

                        case "DATA":
                            await writer.WriteAsync("354 End the data with <CRLF>.<CRLF>\r\n");

                            var body = new StringBuilder();

                            while (true)
                            {
                                var data = await reader.ReadLineAsync();

                                if (data is null || data == ".")
                                {
                                    break;
                                }

                                body.AppendLine(data);
                            }

                            lock (_accepted)
                            {
                                _accepted.Add(body.ToString());
                            }

                            await writer.WriteAsync("250 OK: queued\r\n");
                            break;

                        case "QUIT":
                            await writer.WriteAsync("221 Bye\r\n");
                            return;

                        case "RSET":
                            await writer.WriteAsync("250 OK\r\n");
                            break;

                        default:
                            await writer.WriteAsync("250 OK\r\n");
                            break;
                    }
                }
            }
            catch (IOException)
            {
                // The caller closed the connection before the conversation was
                // over.
            }
            catch (OperationCanceledException)
            {
                // The sink is shutting down.
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _shutdown.CancelAsync();
        _listener.Stop();
        _shutdown.Dispose();
    }
}
