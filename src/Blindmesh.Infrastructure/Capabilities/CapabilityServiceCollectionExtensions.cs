using Blindmesh.Application.Capabilities;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Capabilities;

/// <summary>
/// Registers the services of the capabilities module. The store and the service
/// are scoped, so they share the scoped database context of the request.
/// </summary>
public static class CapabilityServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshCapabilities(this IServiceCollection services)
    {
        services.AddScoped<ICapabilityStore, EfCapabilityStore>();
        services.AddScoped<CapabilityService>();

        // The seam that hands the hard conditions of a published task to the
        // module. The task publication package is a later package and replaces
        // this reader with the one that reads a published task.
        services.AddScoped<ITaskConditionReader, NoPublishedTaskConditionReader>();

        return services;
    }
}
