using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Configuration;
using Blindmesh.Infrastructure.Health;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Blindmesh.Infrastructure;

/// <summary>
/// Registers the shared infrastructure of the product: the single EF Core context,
/// the migration stream, the outbox and idempotency seams, the clock and the
/// dependency health checks.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddBlindmeshInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BlindmeshRuntimeOptions>(configuration.GetSection(BlindmeshRuntimeOptions.SectionName));

        var registered = configuration.GetSection(BlindmeshRuntimeOptions.SectionName).Get<BlindmeshRuntimeOptions>()
            ?? new BlindmeshRuntimeOptions();

        if (string.IsNullOrWhiteSpace(registered.Database.ConnectionString))
        {
            throw new InvalidOperationException(
                "Blindmesh:Database:ConnectionString is required. Set it through configuration before the host starts.");
        }

        // The values are read when the service is created, so the final
        // configuration of the host is the one that is used.
        services.AddSingleton(provider =>
        {
            var options = provider.GetRequiredService<IConfiguration>()
                .GetSection(BlindmeshRuntimeOptions.SectionName)
                .Get<BlindmeshRuntimeOptions>() ?? new BlindmeshRuntimeOptions();

            if (string.IsNullOrWhiteSpace(options.Database.ConnectionString))
            {
                throw new InvalidOperationException(
                    "Blindmesh:Database:ConnectionString is required. Set it through configuration before the host starts.");
            }

            return NpgsqlDataSource.Create(options.Database.ConnectionString);
        });

        services.AddDbContext<BlindmeshDbContext>((provider, optionsBuilder) =>
            optionsBuilder.UseNpgsql(
                provider.GetRequiredService<NpgsqlDataSource>(),
                npgsql => npgsql.MigrationsAssembly(typeof(BlindmeshDbContext).Assembly.GetName().Name)));

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<OutboxRepository>();
        services.AddScoped<IOutboxWriter>(provider => provider.GetRequiredService<OutboxRepository>());
        services.AddScoped<IIdempotencyStore, IdempotencyRepository>();
        services.AddScoped<ICommandTransactionFactory, EfCommandTransactionFactory>();
        services.AddScoped<DatabaseMigrator>();

        services.AddHttpClient("object-storage", (provider, client) =>
        {
            var options = provider.GetRequiredService<IConfiguration>()
                .GetSection(BlindmeshRuntimeOptions.SectionName)
                .Get<BlindmeshRuntimeOptions>() ?? new BlindmeshRuntimeOptions();

            client.Timeout = TimeSpan.FromSeconds(3);

            if (!string.IsNullOrWhiteSpace(options.ObjectStorage.Endpoint))
            {
                client.BaseAddress = new Uri(options.ObjectStorage.Endpoint, UriKind.Absolute);
            }
        });

        services.AddHealthChecks()
            .AddCheck<PostgresHealthCheck>("postgres", tags: [ReadyTag])
            .AddCheck<ObjectStorageHealthCheck>("object-storage", tags: [ReadyTag])
            .AddCheck<MailHealthCheck>("mail", tags: [ReadyTag]);

        return services;
    }
}
