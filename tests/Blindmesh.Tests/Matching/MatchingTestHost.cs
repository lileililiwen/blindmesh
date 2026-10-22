using System.Net;
using System.Text.Json;
using Blindmesh.Domain.Capabilities;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Blindmesh.Tests.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Matching;

/// <summary>
/// The host of the matching tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. Every command of a test goes through
/// the real endpoints, so the rule of the scope, the evaluation of the
/// conditions and the order of the list are part of the result.
/// </summary>
internal sealed class MatchingTestHost : IDisposable
{
    private readonly TaskTestHost _host;

    public MatchingTestHost(RuntimeFixture fixture)
    {
        _host = new TaskTestHost(fixture);
    }

    public HttpClient Client => _host.Client;

    /// <summary>A client that keeps no cookie, like a visitor without a session.</summary>
    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null) =>
        _host.SendAsync(method, path, body, idempotencyKey);

    public Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) => _host.WithDatabaseAsync(action);

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    /// <summary>Claims the limited grant of the signed in contributor.</summary>
    public Task ClaimGrantAsync() => _host.ClaimGrantAsync();

    /// <summary>Creates a project of the signed in contributor and activates it.</summary>
    public Task<string> CreateActiveProjectAsync(string name = "Matching project", string visibility = "public") =>
        _host.CreateActiveProjectAsync(name, visibility);

    /// <summary>
    /// Declares a capability for the signed in contributor. The declaration is
    /// public and available by default, so it can be matched against a task.
    /// </summary>
    public async Task<string> DeclareCapabilityAsync(
        string kind,
        string label,
        string[]? tags = null,
        string visibility = "public",
        bool available = true)
    {
        var (_, csrfToken) = await _host.ReadSessionAsync();

        var tagList = tags is null
            ? "[]"
            : $"[{string.Join(",", tags.Select(tag => $"\"{tag}\""))}]";

        var created = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            $"{{\"kind\":\"{kind}\",\"label\":\"{label}\",\"tags\":{tagList}," +
            $"\"visibility\":\"{visibility}\",\"available\":{available.ToString().ToLowerInvariant()}}}",
            null,
            csrfToken);

        var body = await created.Content.ReadAsStringAsync(CancellationToken.None);

        if (created.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"The capability could not be declared: {created.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Writes a task of the project and publishes it. The answer is the id of
    /// the published task.
    /// </summary>
    public async Task<string> PublishTaskAsync(
        string projectId,
        string visibility = "public",
        params (string Kind, string Value)[] requirements)
    {
        var (_, csrfToken) = await _host.ReadSessionAsync();

        var requirementList = requirements.Length == 0
            ? "[]"
            : $"[{string.Join(",", requirements.Select(requirement =>
                $"{{\"kind\":\"{requirement.Kind}\",\"value\":\"{requirement.Value}\"}}"))}]";

        var created = await _host.SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/tasks",
            $$"""
              {
                "project_id":"{{projectId}}",
                "title":"Check the matched scope of the marketplace",
                "scope":"Walk through the matched scope of the marketplace and report what you see.",
                "modes":["verify"],
                "visibility":"{{visibility}}",
                "expected_minutes":30,
                "base_reward":10,
                "slot_bonus":5,
                "slot_count":2,
                "deadline":"{{TaskTestHost.DeadlineInDays()}}",
                "accepts_clean_findings":false,
                "access_kind":null,
                "access_resource":null,
                "review_criteria":null,
                "requirements":{{requirementList}},
                "steps":[{"title":"Open the task page","expected_result":"The page shows the seat."}]
              }
              """,
            null,
            csrfToken);

        var body = await created.Content.ReadAsStringAsync(CancellationToken.None);

        if (created.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"The task could not be created: {created.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);
        var taskId = document.RootElement.GetProperty("id").GetString()!;

        var published = await _host.SendAsync(
            HttpMethod.Post,
            $"/v1/tasks/{taskId}/publish",
            idempotencyKey: $"matching-publish-{Guid.NewGuid():N}");

        if (published.StatusCode != HttpStatusCode.OK)
        {
            var publishBody = await published.Content.ReadAsStringAsync(CancellationToken.None);

            throw new InvalidOperationException(
                $"The task could not be published: {published.StatusCode} {publishBody}");
        }

        return taskId;
    }

    /// <summary>The version of a task that the contributor saw.</summary>
    public Task<int> VersionAsync(string taskId) =>
        WithDatabaseAsync(async dbContext =>
        {
            var task = await dbContext.Tasks.AsNoTracking()
                .FirstAsync(item => item.Id == Guid.Parse(taskId));

            return task.Version;
        });

    /// <summary>The id of the account of a seeded actor.</summary>
    public Task<Guid> UserIdAsync(string actor) =>
        WithDatabaseAsync(async dbContext =>
        {
            var user = await dbContext.Users.AsNoTracking()
                .FirstAsync(item => item.Username == actor);

            return user.Id;
        });

    /// <summary>
    /// The matchable declaration of an account with a label, with the version
    /// the owner saw. Null when the account offers no such declaration.
    /// </summary>
    public Task<(string Id, int Version)?> FindMatchableDeclarationAsync(string actor, string label) =>
        WithDatabaseAsync(async dbContext =>
        {
            var user = await dbContext.Users.AsNoTracking()
                .FirstAsync(item => item.Username == actor);

            var capability = await dbContext.Capabilities.AsNoTracking()
                .Where(item =>
                    item.OwnerId == user.Id &&
                    item.Label == label &&
                    item.Visibility == CapabilityVisibility.Public &&
                    item.Available &&
                    item.Status == CapabilityStatus.Declared)
                .OrderBy(item => item.CreatedAt)
                .FirstOrDefaultAsync();

            if (capability is null)
            {
                return ((string Id, int Version)?)null;
            }

            return (capability.Id.ToString(), capability.Version);
        });

    public void Dispose() => _host.Dispose();
}
