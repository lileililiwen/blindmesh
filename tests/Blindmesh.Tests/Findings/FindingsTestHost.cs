using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Findings;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Blindmesh.Tests.Findings;

/// <summary>
/// The host of the findings tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. Every command of a test goes
/// through the real endpoints, so the transaction, the escrow and the
/// idempotency of the API are part of the result. The private object storage
/// is a stand in, so the test run needs no external service.
/// </summary>
internal sealed class FindingsTestHost : IDisposable
{
    private readonly IdentityTestHost _host;

    public FakeObjectStorage Storage { get; } = new();

    /// <summary>
    /// The host of the tests. The optional callback adds services to the same
    /// container the API runs in, so a package that owns a consumer of the
    /// outbox registers it exactly like the worker of the product does.
    /// </summary>
    public FindingsTestHost(RuntimeFixture fixture, Action<IServiceCollection>? configureServices = null)
    {
        _host = new IdentityTestHost(
            fixture,
            configureServices: services =>
            {
                services.RemoveAll<IObjectStorage>();
                services.AddSingleton<IObjectStorage>(Storage);

                configureServices?.Invoke(services);
            });
    }

    public HttpClient Client => _host.Client;

    /// <summary>
    /// The services of the hosted API. A test that needs the worker of a
    /// command resolves them from the same scope the request uses.
    /// </summary>
    public IServiceProvider Services => _host.Services;

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
    /// Uploads the bytes of a quarantined file and releases it. The request
    /// carries the form of the upload, exactly like the browser does.
    /// </summary>
    public async Task<HttpResponseMessage> UploadAttachmentAsync(
        Guid attachmentId,
        string fileName,
        string contentType,
        byte[] content,
        string? idempotencyKey = null)
    {
        var (_, csrfToken) = await ReadSessionAsync();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(attachmentId.ToString()), "attachment_id");
        form.Add(new ByteArrayContent(content), "file", fileName);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/attachments/complete")
        {
            Content = form,
        };

        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrfToken);

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
        request.Headers.TryAddWithoutValidation("Idempotency-Key", $"findings-grant-{Guid.NewGuid():N}");

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
    public async Task<string> CreateActiveProjectAsync(string name = "Findings project", string visibility = "public")
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var create = await SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/projects",
            $$"""
              {"name":"{{name}}","summary":"A project of the findings tests.","description":"A project of the findings tests.","repository_url":"https://github.com/blindmesh/findings-tests","visibility":"{{visibility}}"}
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

    /// <summary>
    /// Offers a public capability of the signed in contributor, so the
    /// contributor matches the hard conditions of a task.
    /// </summary>
    public async Task OfferCapabilityAsync(string kind = "region", string value = "eu-central")
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var created = await SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/capabilities",
            $"{{\"kind\":\"{kind}\",\"label\":\"{value}\",\"tags\":[\"{value}\"],\"visibility\":\"public\",\"available\":true}}",
            null,
            csrfToken);

        var body = await created.Content.ReadAsStringAsync(CancellationToken.None);

        if (created.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"The capability could not be offered: {created.StatusCode} {body}");
        }
    }

    /// <summary>The body of a task of the tests.</summary>
    public static string TaskBody(
        string projectId,
        string title = "Check the evidence of a seat of a published task",
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
            "scope":"Walk through the seat of the task and report what you see.",
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

    /// <summary>
    /// Runs the sweep of the deadlines of the worker against the isolated
    /// database of the test run. The sweep uses the same application method and
    /// the same lock order as the commands.
    /// </summary>
    public async Task<ReviewSweepReport> RunReviewSweepAsync()
    {
        using var scope = _host.Services.CreateScope();
        var provider = scope.ServiceProvider;

        var findings = provider.GetRequiredService<FindingsService>();
        var transactionFactory = provider.GetRequiredService<ICommandTransactionFactory>();

        await using var transaction = await transactionFactory.BeginAsync(CancellationToken.None);

        var report = await findings.SweepOverdueAsync(CancellationToken.None);

        await transaction.CommitAsync(CancellationToken.None);

        return report;
    }

    public void Dispose() => _host.Dispose();
}
