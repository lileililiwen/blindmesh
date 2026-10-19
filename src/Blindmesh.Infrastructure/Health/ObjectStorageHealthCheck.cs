using Blindmesh.Application.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
namespace Blindmesh.Infrastructure.Health;

/// <summary>
/// Reports the real state of the private object storage by asking its health
/// endpoint. No credential is read and no object is fetched.
/// </summary>
public sealed class ObjectStorageHealthCheck(
    IHttpClientFactory httpClientFactory,
    IOptions<BlindmeshRuntimeOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var storage = options.Value.ObjectStorage;

        if (string.IsNullOrWhiteSpace(storage.Endpoint))
        {
            return HealthCheckResult.Unhealthy("Object storage endpoint is not configured.");
        }

        try
        {
            var client = httpClientFactory.CreateClient("object-storage");
            using var response = await client.GetAsync(storage.HealthPath, cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Object storage answered its health probe.")
                : HealthCheckResult.Unhealthy($"Object storage answered with status {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Object storage is not reachable.", exception);
        }
    }
}
