using Blindmesh.Application.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Infrastructure.Identity;

/// <summary>
/// Registers the identity module. The scoped services stay scoped and the
/// configuration is read when the service is resolved, so the host
/// configuration of a test host is the one that is used.
/// </summary>
public static class IdentityServiceCollectionExtensions
{
    public const string GitHubHttpClientName = "github";

    public static IServiceCollection AddBlindmeshIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IdentityOptions>(configuration.GetSection(IdentityOptions.SectionName));

        services.AddScoped<IIdentityStore, EfIdentityStore>();
        services.AddScoped<IdentityService>();

        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IConfiguration>()
                .GetSection(IdentityOptions.SectionName)
                .Get<IdentityOptions>() ?? new IdentityOptions();

            return new IdentityCookiePolicy(options);
        });

        services.AddSingleton(provider => new LocalGitHubFixtureGate(
            provider.GetRequiredService<IConfiguration>(),
            provider.GetRequiredService<IHostEnvironment>()));

        services.AddHttpClient(GitHubHttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));

        services.AddSingleton<IGitHubIdentityClient>(provider =>
        {
            var options = provider.GetRequiredService<IConfiguration>()
                .GetSection(IdentityOptions.SectionName)
                .Get<IdentityOptions>() ?? new IdentityOptions();

            var gate = provider.GetRequiredService<LocalGitHubFixtureGate>();

            if (gate.IsEnabled)
            {
                return new LocalGitHubFixtureClient(options);
            }

            var httpClient = provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(GitHubHttpClientName);

            return new GitHubOAuthClient(
                httpClient,
                options,
                provider.GetRequiredService<ILogger<GitHubOAuthClient>>());
        });

        return services;
    }
}
