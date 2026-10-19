using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Applies the product migration stream. This is the only place that mutates the
/// schema; the API exposes it through the dedicated migrate command.
/// </summary>
public sealed class DatabaseMigrator(
    BlindmeshDbContext dbContext,
    ILogger<DatabaseMigrator> logger)
{
    public async Task MigrateAsync(CancellationToken cancellationToken)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pending.Length == 0)
        {
            logger.LogInformation("Database schema is already up to date.");
            return;
        }

        logger.LogInformation("Applying {Count} pending migration(s): {Migrations}", pending.Length, string.Join(", ", pending));
        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Database schema is up to date.");
    }
}
