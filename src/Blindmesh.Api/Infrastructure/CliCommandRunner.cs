using Blindmesh.Infrastructure.Persistence;

namespace Blindmesh.Api.Infrastructure;

/// <summary>
/// The commands the API host accepts instead of serving traffic. They keep the
/// schema and the API contract reproducible without a running service.
/// </summary>
public static class CliCommandRunner
{
    public const string MigrateCommand = "migrate";
    public const string OpenApiCommand = "openapi";

    public static async Task<bool> TryRunAsync(string[] args, WebApplication app)
    {
        if (args.Length == 0)
        {
            return false;
        }

        switch (args[0])
        {
            case MigrateCommand:
                await MigrateAsync(app);
                return true;
            case OpenApiCommand:
                await WriteOpenApiAsync(args, app);
                return true;
            default:
                return false;
        }
    }

    private static async Task MigrateAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var migrator = scope.ServiceProvider.GetRequiredService<DatabaseMigrator>();
        await migrator.MigrateAsync(CancellationToken.None);
    }

    private static async Task WriteOpenApiAsync(string[] args, WebApplication app)
    {
        var outputPath = ResolveOutputPath(args);

        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();

        try
        {
            var address = app.Urls.FirstOrDefault()
                ?? throw new InvalidOperationException("The API did not publish a listening address.");

            using var client = new HttpClient { BaseAddress = new Uri(address, UriKind.Absolute) };
            using var response = await client.GetAsync("/v1/openapi/v1.json", CancellationToken.None);
            response.EnsureSuccessStatusCode();

            var bytes = await response.Content.ReadAsByteArrayAsync(CancellationToken.None);

            var directory = Path.GetDirectoryName(Path.GetFullPath(outputPath));

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(outputPath, bytes, CancellationToken.None);
            Console.Out.WriteLine($"Wrote {bytes.Length} bytes of OpenAPI to {Path.GetFullPath(outputPath)}.");
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static string ResolveOutputPath(string[] args)
    {
        if (args.Length >= 3 && string.Equals(args[1], "--output", StringComparison.Ordinal))
        {
            return args[2];
        }

        if (args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal))
        {
            return args[1];
        }

        return Path.Combine("packages", "api-client", "openapi.json");
    }
}
