using Blindmesh.Application.Contracts;
using Blindmesh.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Blindmesh.Api.Endpoints;

/// <summary>
/// Liveness and readiness of the runtime. Liveness reports the process only.
/// Readiness reports the real state of every required dependency.
/// </summary>
public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/v1/health/live", (IClock clock) => Results.Json(new RuntimeLiveness
        {
            Status = "alive",
            CheckedAt = clock.UtcNow,
        }))
        .WithName("HealthLive")
        .WithSummary("Liveness of the API process.")
        .Produces<RuntimeLiveness>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);

        app.MapGet("/v1/health/ready", async (
            HealthCheckService healthCheckService,
            IClock clock,
            CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(
                registration => registration.Tags.Contains(Blindmesh.Infrastructure.InfrastructureServiceCollectionExtensions.ReadyTag),
                cancellationToken);

            var dependencies = report.Entries
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => new RuntimeDependency
                {
                    Name = entry.Key,
                    Status = entry.Value.Status == HealthStatus.Healthy ? "up" : "down",
                    Detail = entry.Value.Description,
                })
                .ToArray();

            var available = report.Status == HealthStatus.Healthy;

            var body = new RuntimeAvailability
            {
                Status = available ? "available" : "unavailable",
                CheckedAt = clock.UtcNow,
                Dependencies = dependencies,
            };

            return Results.Json(
                body,
                statusCode: available ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("HealthReady")
        .WithSummary("Readiness of the API and its required dependencies.")
        .Produces<RuntimeAvailability>(StatusCodes.Status200OK)
        .Produces<RuntimeAvailability>(StatusCodes.Status503ServiceUnavailable)
        .Produces<ApiErrorResponse>(StatusCodes.Status500InternalServerError);
    }
}
