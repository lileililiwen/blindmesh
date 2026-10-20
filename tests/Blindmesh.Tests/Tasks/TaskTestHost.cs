using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Tasks;

/// <summary>
/// The host of the task tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. Every command of a test goes
/// through the real endpoints, so the transaction, the escrow and the
/// idempotency of the API are part of the result.
/// </summary>
internal sealed class TaskTestHost : IDisposable
{
    private readonly IdentityTestHost _host;

    public TaskTestHost(RuntimeFixture fixture)
    {
        _host = new IdentityTestHost(fixture);
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync() => _host.ReadSessionAsync();

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    /// <summary>Runs an action against the isolated database of the test run.</summary>
    public async Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) =>
        await _host.WithDatabaseAsync(async dbContext =>
        {
            await action(dbContext);
            return true;
        });

    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    /// <summary>Sends a request with the CSRF token of the session.</summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null)
    {
        return SendWithTokenAsync(method, path, body, idempotencyKey, null);
    }

    /// <summary>
    /// Sends a request with the CSRF token of the session and with an explicit
    /// idempotency key.
    /// </summary>
    public async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpMethod method,
        string path,
        string? body,
        string? idempotencyKey,
        string? csrfToken)
    {
        var token = csrfToken;

        if (token is null)
        {
            (_, token) = await ReadSessionAsync();
        }

        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", token);

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await Client.SendAsync(request, CancellationToken.None);
    }

    /// <summary>
    /// Claims the limited grant of the signed in contributor, so the account
    /// holds the credits a publication reserves.
    /// </summary>
    public async Task ClaimGrantAsync()
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/wallet/grant")
        {
            Content = new StringContent("{\"rules_accepted\":true}", Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrfToken);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"task-grant-{Guid.NewGuid():N}");

        var response = await Client.SendAsync(request, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"The grant could not be claimed: {response.StatusCode} {body}");
        }
    }

    /// <summary>
    /// Creates a project of the signed in contributor and activates it, so the
    /// project accepts tasks.
    /// </summary>
    public async Task<string> CreateActiveProjectAsync(string name = "Task project", string visibility = "public")
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var create = await SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/projects",
            $$"""
              {"name":"{{name}}","summary":"A project of the task tests.","description":"A project of the task tests.","repository_url":"https://github.com/blindmesh/task-tests","visibility":"{{visibility}}"}
              """,
            null,
            csrfToken);

        var createBody = await create.Content.ReadAsStringAsync(CancellationToken.None);

        if (create.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"The project could not be created: {create.StatusCode} {createBody}");
        }

        using var document = JsonDocument.Parse(createBody);
        var projectId = document.RootElement.GetProperty("id").GetString()!;
        var version = document.RootElement.GetProperty("version").GetInt32();

        var transition = await SendWithTokenAsync(
            HttpMethod.Post,
            $"/v1/projects/{projectId}/transitions",
            $"{{\"expected_version\":{version},\"to\":\"active\"}}",
            null,
            csrfToken);

        var transitionBody = await transition.Content.ReadAsStringAsync(CancellationToken.None);

        if (transition.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException(
                $"The project could not be activated: {transition.StatusCode} {transitionBody}");
        }

        return projectId;
    }

    /// <summary>The body of a task of the tests.</summary>
    public static string TaskBody(
        string projectId,
        string title = "Check the sign in of the marketplace",
        string[]? modes = null,
        string visibility = "public",
        int expectedMinutes = 30,
        int baseReward = 10,
        int slotBonus = 5,
        int slotCount = 2,
        string deadline = "",
        bool acceptsCleanFindings = false,
        string? accessKind = null,
        string? accessResource = null,
        string? reviewCriteria = null,
        string[]? requirements = null,
        string[]? steps = null)
    {
        var selectedModes = modes ?? ["verify"];

        return $$"""
          {
            "project_id":"{{projectId}}",
            "title":"{{title}}",
            "scope":"Walk through the sign in of the marketplace and report what you see.",
            "modes":[{{string.Join(",", selectedModes.Select(mode => $"\"{mode}\""))}}],
            "visibility":"{{visibility}}",
            "expected_minutes":{{expectedMinutes}},
            "base_reward":{{baseReward}},
            "slot_bonus":{{slotBonus}},
            "slot_count":{{slotCount}},
            "deadline":"{{deadline}}",
            "accepts_clean_findings":{{(acceptsCleanFindings ? "true" : "false")}},
            "access_kind":{{(accessKind is null ? "null" : $"\"{accessKind}\"")}},
            "access_resource":{{(accessResource is null ? "null" : $"\"{accessResource}\"")}},
            "review_criteria":{{(reviewCriteria is null ? "null" : $"\"{reviewCriteria}\"")}},
            "requirements":[{{(requirements is null ? "" : string.Join(",", requirements))}}],
            "steps":[{{(steps is null ? "" : string.Join(",", steps))}}]
          }
          """;
    }

    /// <summary>A deadline that lies in the allowed window of a publication.</summary>
    public static string DeadlineInDays(int days = 7) =>
        DateTimeOffset.UtcNow.AddDays(days).ToString("O");

    public void Dispose() => _host.Dispose();
}
