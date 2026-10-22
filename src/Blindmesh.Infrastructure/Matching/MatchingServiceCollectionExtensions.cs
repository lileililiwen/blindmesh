using Blindmesh.Application.Matching;
using Blindmesh.Application.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Matching;

/// <summary>
/// Registers the services of the matching module. The store and the service
/// are scoped, so they share the scoped database context of the request. The
/// module owns no table and adds no migration.
/// </summary>
public static class MatchingServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshMatching(this IServiceCollection services)
    {
        services.AddScoped<IMatchingStore, EfMatchingStore>();
        services.AddScoped<MatchingService>();

        // The seam of the matching module for the task, the project and the
        // eligibility. It is the one method that applies the rule of the scope,
        // so a task that the list of the marketplace does not show is never
        // readable through another route.
        services.AddScoped<ITaskMatchSeam>(provider => provider.GetRequiredService<MatchingService>());

        return services;
    }
}
