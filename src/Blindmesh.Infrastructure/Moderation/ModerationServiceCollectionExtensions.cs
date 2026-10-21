using Blindmesh.Application.Findings;
using Blindmesh.Application.Identity;
using Blindmesh.Application.Moderation;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Infrastructure.Moderation;

/// <summary>
/// Registers the services of the moderation module. The store and the service
/// are scoped, so they share the scoped database context of the request and
/// the transaction the caller opened. The reader of the standing of an account
/// is scoped as well, because it reads the row of the account.
/// </summary>
public static class ModerationServiceCollectionExtensions
{
    public static IServiceCollection AddBlindmeshModeration(this IServiceCollection services)
    {
        services.AddScoped<IModerationStore, EfModerationStore>();
        services.AddScoped<ModerationService>();
        services.AddScoped<IAccountStandingReader, EfAccountStandingReader>();
        services.AddScoped<IFindingsDisputeSeam, FindingsDisputeSeam>();

        return services;
    }
}
