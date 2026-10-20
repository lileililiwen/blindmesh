using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Projects;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Projects;

/// <summary>
/// PROJ-2 Visibility: a member of a private project may read it but not
/// change it, a contributor without a relation does not learn that the project
/// exists, and a removed member loses the access by the next read.
/// </summary>
[Collection(ProjectCollection.Name)]
public sealed class Proj2VisibilityTests
{
    private readonly RuntimeFixture _fixture;

    public Proj2VisibilityTests(RuntimeFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task PROJ2SuccessAMemberReadsThePrivateProjectAndMayNotChangeIt()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Secret project"),
                ("summary", "Only the team may read it"),
                ("repository_url", "https://github.com/alice/secret"),
                ("visibility", "private")));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

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

        using var memberDocument = await host.ReadJsonAsync(member);
        var members = memberDocument.RootElement.GetProperty("members");

        Assert.Equal(1, members.GetArrayLength());
        Assert.Equal("bob", members[0].GetProperty("username").GetString());
        Assert.Equal("viewer", members[0].GetProperty("role").GetString());

        // The member reads the private project.
        await host.SignInAsync("bob");

        var read = await host.Client.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using var readDocument = await host.ReadJsonAsync(read);
        Assert.Equal("Secret project", readDocument.RootElement.GetProperty("name").GetString());

        // The member may not change the content of the project.
        var refused = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("name", "Changed by bob"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // The refused change left the project as it was.
        var unchanged = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.Projects.AsNoTracking().FirstAsync(item => item.Id == projectId));

        Assert.Equal("Secret project", unchanged.Name);
        Assert.Equal(1, unchanged.Version);
    }

    [Fact]
    public async Task PROJ2RejectionAnUnrelatedContributorGetsNoProjectAndNoDetail()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Hidden project"),
                ("summary", "A summary that must not leak"),
                ("description", "A description that must not leak"),
                ("repository_url", "https://github.com/alice/hidden"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        // Cara has no relation to the project.
        await host.SignInAsync("cara");

        var read = await host.Client.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var body = await read.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("Hidden project", body, StringComparison.Ordinal);
        Assert.DoesNotContain("must not leak", body, StringComparison.Ordinal);
        Assert.DoesNotContain("github.com/alice/hidden", body, StringComparison.Ordinal);

        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("error");

        Assert.Equal("not_found", error.GetProperty("code").GetString());
        Assert.False(error.TryGetProperty("field_errors", out _));

        // The list of cara does not hold the project of alice either.
        var list = await host.Client.GetAsync("/v1/projects", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        using var listDocument = await host.ReadJsonAsync(list);
        var items = listDocument.RootElement.GetProperty("items");

        foreach (var item in items.EnumerateArray())
        {
            Assert.NotEqual(projectId, item.GetProperty("id").GetGuid());
        }
    }

    [Fact]
    public async Task PROJ2BoundaryTheRemovedMemberLosesTheAccessByTheNextRead()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Temporary team"),
                ("repository_url", "https://github.com/alice/team"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("bob");
        await host.SignInAsync("alice");

        var member = await host.SendUnsafeAsync(
            HttpMethod.Put,
            $"/v1/projects/{projectId}/members/bob",
            ProjectRequest.Body(("role", "editor")));

        Assert.Equal(HttpStatusCode.OK, member.StatusCode);

        // The editor may change the content of the project.
        await host.SignInAsync("bob");

        var edited = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("summary", "Edited by the editor"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);

        // The owner removes the member again.
        await host.SignInAsync("alice");

        var removed = await host.SendUnsafeAsync(
            HttpMethod.Delete,
            $"/v1/projects/{projectId}/members/bob",
            null);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var memberRows = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.ProjectMembers.CountAsync(item => item.ProjectId == projectId));

        Assert.Equal(0, memberRows);

        // The next read of the removed member does not find the project.
        await host.SignInAsync("bob");

        var read = await host.Client.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        var write = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("name", "Changed after the removal"), ("expected_version", 2)));

        Assert.Equal(HttpStatusCode.NotFound, write.StatusCode);
    }

    [Fact]
    public async Task PROJ2BoundaryOnlyTheOwnerChangesTheVisibilityAndAPublicProjectIsReadable()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Open project"),
                ("repository_url", "https://github.com/alice/open"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        // A visitor without a session does not see the private project.
        var anonymous = host.CreateClientWithoutCookies();

        var hidden = await anonymous.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        // The owner makes the project public.
        var published = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("visibility", "public"), ("expected_version", 1)));

        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        using var publishedDocument = await host.ReadJsonAsync(published);
        Assert.Equal("public", publishedDocument.RootElement.GetProperty("visibility").GetString());

        // The public project is readable without a session.
        var visible = await anonymous.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, visible.StatusCode);

        using var visibleDocument = await host.ReadJsonAsync(visible);
        var visibleRoot = visibleDocument.RootElement;

        Assert.Equal("Open project", visibleRoot.GetProperty("name").GetString());

        // The public project carries no membership of another contributor.
        Assert.Equal(0, visibleRoot.GetProperty("members").GetArrayLength());

        // The owner makes the project private again.
        var hiddenAgain = await host.SendUnsafeAsync(
            HttpMethod.Patch,
            $"/v1/projects/{projectId}",
            ProjectRequest.Body(("visibility", "private"), ("expected_version", 2)));

        Assert.Equal(HttpStatusCode.OK, hiddenAgain.StatusCode);

        // The next read of the visitor does not find the project any more.
        var hiddenAfter = await anonymous.GetAsync($"/v1/projects/{projectId}", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, hiddenAfter.StatusCode);
    }

    [Fact]
    public async Task PROJ2BoundaryOnlyTheOwnerManagesTheMembers()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Owned project"),
                ("repository_url", "https://github.com/alice/owned"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        await host.SignInAsync("bob");
        await host.SignInAsync("alice");

        await host.SendUnsafeAsync(
            HttpMethod.Put,
            $"/v1/projects/{projectId}/members/bob",
            ProjectRequest.Body(("role", "editor")));

        // The editor may not manage the members.
        await host.SignInAsync("bob");

        var refused = await host.SendUnsafeAsync(
            HttpMethod.Put,
            $"/v1/projects/{projectId}/members/cara",
            ProjectRequest.Body(("role", "viewer")));

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        var memberRows = await host.WithDatabaseAsync(async dbContext =>
            await dbContext.ProjectMembers.CountAsync(item => item.ProjectId == projectId));

        Assert.Equal(1, memberRows);
    }

    [Fact]
    public async Task PROJ2BoundaryTheListHoldsTheVisibleProjectsInAStableOrder()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var created = await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Listable project"),
                ("repository_url", "https://github.com/alice/listable"),
                ("visibility", "private")));

        using var createdDocument = await host.ReadJsonAsync(created);
        var projectId = createdDocument.RootElement.GetProperty("id").GetGuid();

        // Cara owns a project that alice may not see.
        await host.SignInAsync("cara");

        await host.SendUnsafeAsync(
            HttpMethod.Post,
            "/v1/projects",
            ProjectRequest.Body(
                ("name", "Project of cara"),
                ("repository_url", "https://github.com/cara/own"),
                ("visibility", "private")));

        await host.SignInAsync("alice");

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var path = "/v1/projects?limit=1";

            if (cursor is not null)
            {
                path += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await host.Client.GetAsync(path, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = await host.ReadJsonAsync(response);
            var items = document.RootElement.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                seen.Add(item.GetProperty("id").GetGuid());
            }

            cursor = document.RootElement.TryGetProperty("next_cursor", out var next)
                ? next.GetString()
                : null;

            pages += 1;
        }
        while (cursor is not null && pages < 50);

        // Every project is listed once and the project of cara is not listed.
        Assert.Equal(seen.Count, seen.Distinct().Count());
        Assert.Contains(projectId, seen);
    }

    [Fact]
    public async Task PROJ2BoundaryTheListKeepsEveryProjectWhenTheyShareTheCreationTime()
    {
        using var host = new IdentityTestHost(_fixture);

        await host.SignInAsync("alice");

        var alice = await host.FindUserBySubjectAsync("1001");

        Assert.NotNull(alice);

        var createdAt = DateTimeOffset.UtcNow;

        var expected = await host.WithDatabaseAsync(async dbContext =>
        {
            var ids = new List<Guid>();

            for (var index = 0; index < 3; index++)
            {
                var project = new Project
                {
                    Id = Guid.NewGuid(),
                    OwnerId = alice!.Id,
                    Name = $"Shared creation time {index}",
                    Summary = string.Empty,
                    Description = string.Empty,
                    RepositoryUrl = "https://github.com/alice/shared",
                    Visibility = ProjectVisibility.Private,
                    Status = ProjectStatus.Draft,
                    Version = 1,
                    CreatedAt = createdAt,
                    UpdatedAt = createdAt,
                };

                dbContext.Projects.Add(project);
                ids.Add(project.Id);
            }

            await dbContext.SaveChangesAsync();

            return ids;
        });

        var seen = new List<Guid>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var path = "/v1/projects?limit=1";

            if (cursor is not null)
            {
                path += $"&cursor={Uri.EscapeDataString(cursor)}";
            }

            var response = await host.Client.GetAsync(path, CancellationToken.None);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var document = await host.ReadJsonAsync(response);

            foreach (var item in document.RootElement.GetProperty("items").EnumerateArray())
            {
                seen.Add(item.GetProperty("id").GetGuid());
            }

            cursor = document.RootElement.TryGetProperty("next_cursor", out var next)
                ? next.GetString()
                : null;

            pages += 1;
        }
        while (cursor is not null && pages < 100);

        // The projects that share the creation time are listed once each.
        Assert.Equal(seen.Count, seen.Distinct().Count());

        foreach (var id in expected)
        {
            Assert.Contains(id, seen);
        }
    }
}
