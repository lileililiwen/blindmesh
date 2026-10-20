using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Projects;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Projects;

/// <summary>
/// PROJ-1 Lifecycle: the owner activates a draft, records a version, the
/// history of the lifecycle is kept, an illegal transition is refused and the
/// archive keeps the activity of the project.
/// </summary>
[Collection(ProjectCollection.Name)]
public sealed class Proj1LifecycleTests
{
    private readonly RuntimeFixture _fixture;

    public Proj1LifecycleTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PROJ1SuccessTheOwnerActivatesTheDraftAndTheVersionHistoryIsRecorded()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Alpha"),
                ("summary", "The first project of alice"),
                ("description", "A longer description of the project."),
                ("repository_url", "https://github.com/alice/alpha"),
                ("visibility", "private")));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdDocument = await host.ReadJsonAsync(created);
        var root = createdDocument.RootElement;
        var projectId = root.GetProperty("id").GetGuid();

        Assert.Equal("Alpha", root.GetProperty("name").GetString());
        Assert.Equal("draft", root.GetProperty("status").GetString());
        Assert.Equal("private", root.GetProperty("visibility").GetString());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.False(root.GetProperty("accepts_tasks").GetBoolean());
        Assert.Equal(0, root.GetProperty("status_history").GetArrayLength());
        Assert.Equal(0, root.GetProperty("versions").GetArrayLength());

        // The owner activates the draft.
        var activated = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            ProjectRequest.Body(("to", "active"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, activated.StatusCode);

        using var activatedDocument = await host.ReadJsonAsync(activated);
        var activatedRoot = activatedDocument.RootElement;

        Assert.Equal("active", activatedRoot.GetProperty("status").GetString());
        Assert.Equal(2, activatedRoot.GetProperty("version").GetInt32());
        Assert.True(activatedRoot.GetProperty("accepts_tasks").GetBoolean());

        var history = activatedRoot.GetProperty("status_history");
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal("draft", history[0].GetProperty("from_status").GetString());
        Assert.Equal("active", history[0].GetProperty("to_status").GetString());
        Assert.Equal("alice", history[0].GetProperty("changed_by").GetString());

        // The owner records the first released version.
        var versioned = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/versions",
            ProjectRequest.Body(("label", "v1.0.0"), ("commit_reference", "0f1e2d3")));

        Assert.Equal(HttpStatusCode.Created, versioned.StatusCode);

        using var versionedDocument = await host.ReadJsonAsync(versioned);
        var versionedRoot = versionedDocument.RootElement;

        var versions = versionedRoot.GetProperty("versions");
        Assert.Equal(1, versions.GetArrayLength());
        Assert.Equal("v1.0.0", versions[0].GetProperty("label").GetString());
        Assert.Equal("0f1e2d3", versions[0].GetProperty("commit_reference").GetString());

        // The read answers the committed state.
        var read = await host.Client.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var readDocument = await host.ReadJsonAsync(read);
        var readRoot = readDocument.RootElement;

        Assert.Equal("active", readRoot.GetProperty("status").GetString());
        Assert.Equal(1, readRoot.GetProperty("versions").GetArrayLength());
        Assert.Equal(1, readRoot.GetProperty("status_history").GetArrayLength());

        // The rows of the history and of the version are in the database.
        var rows = await host.WithDatabaseAsync(async dbContext => new
        {
            History = await dbContext.ProjectStatusChanges.CountAsync(item => item.ProjectId == projectId),
            Versions = await dbContext.ProjectVersions.CountAsync(item => item.ProjectId == projectId),
            Projects = await dbContext.Projects.CountAsync(item => item.Id == projectId),
        });

        Assert.Equal(1, rows.History);
        Assert.Equal(1, rows.Versions);
        Assert.Equal(1, rows.Projects);
    }

    [Fact]
    public async Task PROJ1RejectionAViewerMayNotEditAndAnIllegalTransitionIsRefused()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Beta"),
                ("repository_url", "https://github.com/alice/beta"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        // The owner adds bob as a viewer.
        await host.SignInAsync("bob");
        await host.SignInAsync("alice");

        var member = await host.SendUnsafeAsync(
            HttpMethod.Put,
            $"/v1/projects/{projectId}/members/bob",
            ProjectRequest.Body(("role", "viewer")));

        Assert.Equal(HttpStatusCode.OK, member.StatusCode);

        // The viewer may not change the content of the project.
        await host.SignInAsync("bob");

        var refused = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("name", "Beta by bob"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        using var refusedDocument = await host.ReadJsonAsync(refused);
        Assert.Equal("forbidden", refusedDocument.RootElement.GetProperty("error").GetProperty("code").GetString());

        // The refused change left the project as it was.
        var unchanged = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking().FirstAsync(item => item.Id == projectId));

        Assert.Equal("Beta", unchanged.Name);
        Assert.Equal(1, unchanged.Version);
        Assert.Equal(ProjectStatus.Draft, unchanged.Status);

        // A draft cannot be paused.
        await host.SignInAsync("alice");

        var illegal = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            ProjectRequest.Body(("to", "paused"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);

        using var illegalDocument = await host.ReadJsonAsync(illegal);
        Assert.Equal(
            "invalid_transition",
            illegalDocument.RootElement.GetProperty("error").GetProperty("code").GetString());

        var afterIllegal = await host.WithDatabaseAsync(async dbContext => new
        {
            Project = await dbContext.Projects.AsNoTracking().FirstAsync(item => item.Id == projectId),
            History = await dbContext.ProjectStatusChanges.CountAsync(item => item.ProjectId == projectId),
        });

        Assert.Equal(ProjectStatus.Draft, afterIllegal.Project.Status);
        Assert.Equal(1, afterIllegal.Project.Version);
        Assert.Equal(0, afterIllegal.History);
    }

    [Fact]
    public async Task PROJ1BoundaryTheArchiveKeepsTheActivityAndRefusesNewVersions()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Gamma"),
                ("repository_url", "https://github.com/alice/gamma"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            ProjectRequest.Body(("to", "active"), ("expected_version", 1)));

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/versions",
            ProjectRequest.Body(("label", "v1.0.0")));

        var archived = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            ProjectRequest.Body(("to", "archived"), ("expected_version", 3)));

        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        using var archivedDocument = await host.ReadJsonAsync(archived);
        var archivedRoot = archivedDocument.RootElement;

        Assert.Equal("archived", archivedRoot.GetProperty("status").GetString());
        Assert.False(archivedRoot.GetProperty("accepts_tasks").GetBoolean());
        Assert.Equal(2, archivedRoot.GetProperty("status_history").GetArrayLength());
        Assert.Equal(1, archivedRoot.GetProperty("versions").GetArrayLength());

        // The archive keeps the activity of the project.
        var rows = await host.WithDatabaseAsync(async dbContext => new
        {
            History = await dbContext.ProjectStatusChanges.CountAsync(item => item.ProjectId == projectId),
            Versions = await dbContext.ProjectVersions.CountAsync(item => item.ProjectId == projectId),
        });

        Assert.Equal(2, rows.History);
        Assert.Equal(1, rows.Versions);

        // An archived project accepts no new version and no new content.
        var newVersion = await host.SendUnsafeAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/versions",
            ProjectRequest.Body(("label", "v2.0.0")));

        Assert.Equal(HttpStatusCode.Conflict, newVersion.StatusCode);

        var newContent = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("name", "Gamma again"), ("expected_version", 4)));

        Assert.Equal(HttpStatusCode.Conflict, newContent.StatusCode);

        var unchanged = await host.WithDatabaseAsync(async dbContext => new
        {
            Project = await dbContext.Projects.AsNoTracking().FirstAsync(item => item.Id == projectId),
            Versions = await dbContext.ProjectVersions.CountAsync(item => item.ProjectId == projectId),
        });

        Assert.Equal("Gamma", unchanged.Project.Name);
        Assert.Equal(ProjectStatus.Archived, unchanged.Project.Status);
        Assert.Equal(1, unchanged.Versions);
    }

    [Fact]
    public async Task PROJ1BoundaryAStaleExpectedVersionIsRefusedWithTheCurrentVersion()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Delta"),
                ("repository_url", "https://github.com/alice/delta"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        var first = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("summary", "The first summary"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var firstDocument = await host.ReadJsonAsync(first);
        Assert.Equal(2, firstDocument.RootElement.GetProperty("version").GetInt32());

        // The second change carries the version that is no longer current.
        var stale = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("summary", "The second summary"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        using var staleDocument = await host.ReadJsonAsync(stale);
        var error = staleDocument.RootElement.GetProperty("error");

        Assert.Equal("version_conflict", error.GetProperty("code").GetString());
        Assert.Contains("2", error.GetProperty("message").GetString()!, StringComparison.Ordinal);

        // The refused change left the project as it was.
        var unchanged = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking().FirstAsync(item => item.Id == projectId));

        Assert.Equal("The first summary", unchanged.Summary);
        Assert.Equal(2, unchanged.Version);
    }
}
