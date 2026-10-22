using Blindmesh.Application.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Tasks;

/// <summary>
/// Registers the services of the tasks module. The store and the service are
/// scoped, so they share the scoped database context of the request and the
/// transaction the caller opened.
/// </summary>
public static class TaskServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshTasks(this IServiceCollection services)
    {
        services.AddScoped<ITaskStore, EfTaskStore>();
        services.AddScoped<IParticipationStore, EfParticipationStore>();
        services.AddScoped<TaskService>();
        services.AddScoped<ParticipationService>();

        // The seam that hands the hard conditions of a task to the matching
        // module. The reader returns the conditions without a scope rule, so
        // the matching module never depends on the reader that applies it.
        services.AddScoped<ITaskRequirementsReader, TaskRequirementsReader>();

        return services;
    }
}
