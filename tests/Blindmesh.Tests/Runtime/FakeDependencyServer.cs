using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// A local stand in for the object storage and the mail sink. It answers the
/// storage health probe with 200 and accepts the mail connection, so the
/// readiness check can be exercised without an external service.
/// </summary>
public sealed class FakeDependencyServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _shutdown = new();

    public FakeDependencyServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _ = AcceptAsync();
    }

    public int Port { get; }

    public string Endpoint => $"http://127.0.0.1:{Port}";

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

    private static async Task RespondAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();

                // The request line is read, so the probe is answered as soon as
                // it arrives. A client that only connects and closes, like the
                // mail probe, ends the read with null.
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                await reader.ReadLineAsync();

                var response = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
                await stream.FlushAsync();
            }
            catch (IOException)
            {
                // The caller closed the connection before the probe finished.
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
