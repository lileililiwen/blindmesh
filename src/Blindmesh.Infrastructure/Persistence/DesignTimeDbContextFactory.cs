using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Creates the context for the migration tooling without starting the API host.
/// The connection string is a placeholder: migrations are applied by the API
/// migrate command, never by the tooling. The tooling still needs a reachable
/// database for the commands that read the history of the migrations, so the
/// placeholder may be replaced with the environment variable
/// <c>BLINDMESH_DESIGN_CONNECTION_STRING</c>.
/// </summary>
public sealed class BlindmeshDbContextFactory : IDesignTimeDbContextFactory<BlindmeshDbContext>
{
    public BlindmeshDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("BLINDMESH_DESIGN_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=blindmesh_design;Username=blindmesh;Password=blindmesh";

        var optionsBuilder = new DbContextOptionsBuilder<BlindmeshDbContext>();

        optionsBuilder.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(typeof(BlindmeshDbContext).Assembly.GetName().Name));

        return new BlindmeshDbContext(optionsBuilder.Options);
    }
}
