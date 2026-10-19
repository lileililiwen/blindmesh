using System.Net.Sockets;
using Blindmesh.Application.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Blindmesh.Infrastructure.Health;

/// <summary>Reports the real state of the mail sink by opening a TCP connection.</summary>
public sealed class MailHealthCheck(IOptions<BlindmeshRuntimeOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var mail = options.Value.Mail;

        if (string.IsNullOrWhiteSpace(mail.Host))
        {
            return HealthCheckResult.Unhealthy("Mail host is not configured.");
        }

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(mail.Host, mail.Port, cancellationToken);

            return HealthCheckResult.Healthy("Mail sink accepted a connection.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Mail sink is not reachable.", exception);
        }
    }
}
