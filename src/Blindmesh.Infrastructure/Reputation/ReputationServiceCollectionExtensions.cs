using Blindmesh.Application.Reputation;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Reputation;

/// <summary>
/// Registers the services of the reputation module. The store and the service
/// are scoped, so they share the scoped database context of the request and
/// the transaction of the worker that applies the event.
/// </summary>
public static class ReputationServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshReputation(this IServiceCollection services)
    {
        services.AddScoped<IReputationStore, EfReputationStore>();
        services.AddScoped<ReputationService>();

        return services;
    }
}
