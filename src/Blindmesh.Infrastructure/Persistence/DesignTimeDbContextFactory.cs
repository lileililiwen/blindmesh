using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Creates the context for the migration tooling without starting the API host.
/// The connection string is a placeholder: migrations are applied by the API
/// migrate command, never by the tooling.
/// </summary>
public sealed class BlindmeshDbContextFactory : IDesignTimeDbContextFactory<BlindmeshDbContext>
{
    public BlindmeshDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BlindmeshDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=blindmesh_design;Username=blindmesh;Password=blindmesh",
            npgsql => npgsql.MigrationsAssembly(typeof(BlindmeshDbContext).Assembly.GetName().Name));

        return new BlindmeshDbContext(optionsBuilder.Options);
    }
}
