using Blindmesh.Application.Capabilities;
using Blindmesh.Application.Tasks;
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
        // module. The reader of the task publication package answers with the
        // conditions of a real task and with null for a task that the actor may
        // not see.
        services.AddScoped<ITaskConditionReader, PublishedTaskConditionReader>();

        // The seam that hands the matchable declarations of every account and
        // the evaluation of one account to the matching module. It is a reader
        // of its own module, so the matching module never depends on the
        // service that reads the conditions of a task.
        services.AddScoped<ICandidateMatchReader, CandidateMatchReader>();

        return services;
    }
}
