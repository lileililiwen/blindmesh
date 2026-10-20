using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// The migration of the participation package adds only the tables of the
/// package and keeps the rows of the earlier packages. The fresh schema is
/// proved by every other test of the run, because the fixture of the tests
/// applies the whole migration stream to an empty database.
/// </summary>
[Collection(ParticipationCollection.Name)]
public sealed class ParticipationMigrationTests
{
    private const string PreviousMigration = "20261020091645_AddTaskPublication";

    private readonly RuntimeFixture _fixture;

    public ParticipationMigrationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PARTTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages()
    {
        var connectionString = await _fixture.CreateDatabaseAsync("blindmesh_participations_migration");

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

        Assert.DoesNotContain("participations", before);
        Assert.Contains("users", before);
        Assert.Contains("tasks", before);
        Assert.Contains("task_slots", before);

        // The migration of the package is applied.
        await migrator.MigrateAsync();

        // The rows of the earlier packages are preserved.
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal("migrated@blindmesh.test", await dbContext.Users.Select(item => item.Email).FirstAsync());

        // The tables of the package exist and hold nothing yet.
        var after = await dbContext.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("participations", after);
        Assert.Contains("applications", after);
        Assert.Contains("slot_leases", after);
        Assert.Contains("participation_history", after);

        Assert.Equal(0, await dbContext.Participations.CountAsync());
        Assert.Equal(0, await dbContext.TaskApplications.CountAsync());
        Assert.Equal(0, await dbContext.SlotLeases.CountAsync());
        Assert.Equal(0, await dbContext.ParticipationStatusChanges.CountAsync());

        // The exclusive lease of a slot is a partial unique index, so a slot
        // never carries two live leases.
        var indexes = await dbContext.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'slot_leases'")
            .ToListAsync();

        Assert.Contains("ux_slot_leases_slot_id_live", indexes);
        Assert.Contains("ux_slot_leases_participation_id_live", indexes);
    }
}
