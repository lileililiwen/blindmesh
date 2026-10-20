using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// The migration of the task publication package adds only the tables of the
/// package and keeps the rows of the earlier packages. The fresh schema is
/// proved by every other test of the run, because the fixture of the tests
/// applies the whole migration stream to an empty database.
/// </summary>
[Collection(TaskCollection.Name)]
public sealed class TaskMigrationTests
{
    private const string PreviousMigration = "20261020071236_AddProviderAccountCreatedAt";

    private readonly RuntimeFixture _fixture;

    public TaskMigrationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TASKTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages()
    {
        var connectionString = await _fixture.CreateDatabaseAsync("blindmesh_tasks_migration");

        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);
        var migrator = dbContext.GetService<IMigrator>();

        // The database holds the schema of the last accepted package.
        await migrator.MigrateAsync(PreviousMigration);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "migrated",
            DisplayName = "migrated",
            GithubLogin = "migrated",
            Email = "migrated@blindmesh.test",
            Timezone = "UTC",
            Skills = [],
            Languages = [],
            Role = UserRole.User,
            IsPublic = true,
            Version = 1,
            CreatedAt = DateTimeOffset.UnixEpoch,
            UpdatedAt = DateTimeOffset.UnixEpoch,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var before = await dbContext.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.DoesNotContain("tasks", before);
        Assert.Contains("users", before);
        Assert.Contains("projects", before);
        Assert.Contains("ledger_entries", before);

        // The migration of the package is applied.
        await migrator.MigrateAsync();

        // The rows of the earlier packages are preserved.
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal("migrated@blindmesh.test", await dbContext.Users.Select(item => item.Email).FirstAsync());

        // The tables of the package exist and hold nothing yet.
        var after = await dbContext.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("tasks", after);
        Assert.Contains("task_requirements", after);
        Assert.Contains("task_steps", after);
        Assert.Contains("task_slots", after);
        Assert.Contains("task_history", after);

        Assert.Equal(0, await dbContext.Tasks.CountAsync());
        Assert.Equal(0, await dbContext.TaskRequirements.CountAsync());
        Assert.Equal(0, await dbContext.TaskSteps.CountAsync());
        Assert.Equal(0, await dbContext.TaskSlots.CountAsync());
        Assert.Equal(0, await dbContext.TaskStatusChanges.CountAsync());
    }
}
