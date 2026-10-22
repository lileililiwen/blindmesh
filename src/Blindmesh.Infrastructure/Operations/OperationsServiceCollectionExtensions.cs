using Blindmesh.Application.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// Registers the services of the operations module. The store, the measurement
/// and the service are scoped, so they share the scoped database context of the
/// request. The restore environment is a singleton, because it holds the
/// connection of the host and no state of a request.
/// </summary>
public static class OperationsServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshOperations(this IServiceCollection services)
    {
        services.AddScoped<IOperationsStore, EfOperationsStore>();
        services.AddScoped<IOperationsMeasurement, EfOperationsMeasurement>();
        services.AddScoped<OperationsService>();
        services.AddSingleton<IRestoreRehearsalEnvironment, PostgresRestoreEnvironment>();

        return services;
    }
}
