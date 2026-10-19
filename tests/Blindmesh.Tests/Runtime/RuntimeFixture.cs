using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Blindmesh.Tests.Runtime;

/// <summary>
/// The isolated PostgreSQL 17 database of the test run. The tests never touch a
/// production database.
/// </summary>
public sealed class RuntimeFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17.11-alpine")
        .WithDatabase("blindmesh_tests")
        .WithUsername("blindmesh")
        .WithPassword("blindmesh")
        .Build();

    public FakeDependencyServer Dependencies { get; } = new();

    public string ConnectionString => _database.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await ApplyMigrationsAsync(ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await Dependencies.DisposeAsync();
        await _database.DisposeAsync();
    }

    public static async Task ApplyMigrationsAsync(string connectionString)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);
        await dbContext.Database.MigrateAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class RuntimeCollection : ICollectionFixture<RuntimeFixture>
{
    public const string Name = "runtime-foundation";
}
