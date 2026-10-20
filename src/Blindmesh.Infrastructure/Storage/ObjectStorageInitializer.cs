using Blindmesh.Application.Findings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Infrastructure.Storage;

/// <summary>
/// Creates the private bucket of the evidence when the host starts. A storage
/// that does not answer does not stop the host: the attachments answer with the
/// state of the service and the rest of the product keeps working.
/// </summary>
public sealed class ObjectStorageInitializer(
    IObjectStorage storage,
    ILogger<ObjectStorageInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!storage.IsConfigured)
        {
            logger.LogInformation("The private object storage is not configured, so the attachments stay unavailable.");
            return;
        }

        try
        {
            await storage.EnsureBucketAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "The private object storage did not answer, so the attachments stay unavailable.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
