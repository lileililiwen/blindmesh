using Blindmesh.Application.Runtime;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// Applies the runtime policy when the host starts. A host that enables a test
/// fixture outside a test environment fails before it accepts a request.
/// </summary>
public sealed class RuntimePolicyHostedService(
    IOptions<Api.Configuration.ApiOptions> options,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<RuntimePolicyHostedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var appEnvironment = configuration["APP_ENV"] ?? environment.EnvironmentName;

        try
        {
            RuntimePolicy.Validate(appEnvironment, options.Value.TestAuthenticationEnabled);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogCritical(exception, "The host refused to start because of the runtime policy.");
            throw;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
