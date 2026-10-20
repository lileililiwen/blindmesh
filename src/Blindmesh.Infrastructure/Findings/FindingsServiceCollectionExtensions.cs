using Blindmesh.Application.Findings;
using Blindmesh.Infrastructure.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Findings;

/// <summary>
/// Registers the services of the findings module. The store and the service are
/// scoped, so they share the scoped database context of the request and the
/// transaction the caller opened. The private object storage is a singleton,
/// because the client of the S3 API is thread safe and it holds no state of a
/// request.
/// </summary>
public static class FindingsServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshFindings(this IServiceCollection services)
    {
        services.AddScoped<IFindingsStore, EfFindingsStore>();
        services.AddScoped<FindingsService>();
        services.AddSingleton<IObjectStorage, S3ObjectStorage>();

        return services;
    }
}
