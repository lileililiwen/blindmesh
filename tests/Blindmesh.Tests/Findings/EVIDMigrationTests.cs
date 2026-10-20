using Blindmesh.Domain.Identity;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// The migration of the findings package adds only the tables of the package and
/// keeps the rows of the earlier packages. The fresh schema is proved by every
/// other test of the run, because the fixture of the tests applies the whole
/// migration stream to an empty database.
/// </summary>
[Collection(FindingsCollection.Name)]
public sealed class EVIDMigrationTests
{
    private const string PreviousMigration = "20261020145913_AddParticipationExecution";

    private readonly RuntimeFixture _fixture;

    public EVIDMigrationTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task EVIDTheMigrationAddsTheTablesOfThePackageAndKeepsTheRowsOfTheEarlierPackages()
    {
        var connectionString = await _fixture.CreateDatabaseAsync("blindmesh_findings_migration");

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

        Assert.DoesNotContain("submissions", before);
        Assert.Contains("users", before);
        Assert.Contains("tasks", before);
        Assert.Contains("participations", before);

        // The migration of the package is applied.
        await migrator.MigrateAsync();

        // The rows of the earlier packages are preserved.
        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal("migrated@blindmesh.test", await dbContext.Users.Select(item => item.Email).FirstAsync());

        // The tables of the package exist and hold nothing yet.
        var after = await dbContext.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();

        Assert.Contains("submissions", after);
        Assert.Contains("submission_revisions", after);
        Assert.Contains("submission_history", after);
        Assert.Contains("findings", after);
        Assert.Contains("finding_history", after);
        Assert.Contains("attachments", after);

        Assert.Equal(0, await dbContext.Submissions.CountAsync());
        Assert.Equal(0, await dbContext.SubmissionRevisions.CountAsync());
        Assert.Equal(0, await dbContext.SubmissionHistory.CountAsync());
        Assert.Equal(0, await dbContext.Findings.CountAsync());
        Assert.Equal(0, await dbContext.FindingHistory.CountAsync());
        Assert.Equal(0, await dbContext.Attachments.CountAsync());

        // A seat carries one submission and a revision carries one number.
        var indexes = await dbContext.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public' AND tablename IN ('submissions','submission_revisions')")
            .ToListAsync();

        Assert.Contains("ux_submissions_participation_id", indexes);
        Assert.Contains("ux_submission_revisions_submission_id_number", indexes);
    }
}
