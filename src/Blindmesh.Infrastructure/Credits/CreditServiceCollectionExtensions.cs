using Blindmesh.Application.Credits;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Credits;

/// <summary>
/// Registers the services of the credits module. The store and the service are
/// scoped, so they share the scoped database context of the request and the
/// transaction the caller opened.
/// </summary>
public static class CreditServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshCredits(this IServiceCollection services)
    {
        services.AddScoped<ICreditStore, EfCreditStore>();
        services.AddScoped<ICreditService, CreditService>();

        return services;
    }
}
