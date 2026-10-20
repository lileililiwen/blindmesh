using Blindmesh.Application.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Projects;

/// <summary>
/// Registers the services of the projects module. The store and the service
/// are scoped, so they share the scoped database context of the request.
/// </summary>
public static class ProjectServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshProjects(this IServiceCollection services)
    {
        services.AddScoped<IProjectStore, EfProjectStore>();
        services.AddScoped<ProjectService>();

        return services;
    }
}
