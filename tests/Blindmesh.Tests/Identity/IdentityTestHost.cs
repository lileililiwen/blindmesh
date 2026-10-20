using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Tests.Identity;

/// <summary>
/// The host of the identity tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would.
/// </summary>
internal sealed class IdentityTestHost : IDisposable
{
    public const string Origin = "http://localhost:3000";

    public const string SessionCookieName = "blindmesh";

    private readonly BlindmeshApiFactory _factory;

    public IdentityTestHost(
        RuntimeFixture fixture,
        IDictionary<string, string?>? overrides = null,
        Action<IServiceCollection>? configureServices = null)
    {
        Fixture = fixture;

        var settings = new Dictionary<string, string?>
        {
            ["Blindmesh:Database:ConnectionString"] = fixture.ConnectionString,
            ["Blindmesh:ObjectStorage:Endpoint"] = fixture.Dependencies.Endpoint,
            ["Blindmesh:ObjectStorage:HealthPath"] = "/minio/health/live",
            ["Blindmesh:Mail:Host"] = "127.0.0.1",
            ["Blindmesh:Mail:Port"] = fixture.Dependencies.Port.ToString(),
            ["Api:AllowedOrigins:0"] = Origin,
            ["APP_ENV"] = "test",
            ["Blindmesh:Identity:PublicBaseUrl"] = "http://localhost",
            ["Blindmesh:Identity:BrowserApiPrefix"] = "/v1",
            ["Blindmesh:Identity:LocalFixtureEnabled"] = "true",
            ["Blindmesh:Identity:CookieSecurity"] = "Development",
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                settings[pair.Key] = pair.Value;
            }
        }

        _factory = new BlindmeshApiFactory(settings, configureServices: configureServices);
        Client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false,
        });
    }

    public RuntimeFixture Fixture { get; }

    public HttpClient Client { get; }

    /// <summary>
    /// The services of the running API, so a test can call an application
    /// method of the product the same way the worker does.
    /// </summary>
    public IServiceProvider Services => _factory.Services;

    public void Dispose()
    {
        Client.Dispose();
        _factory.Dispose();
    }

    /// <summary>Reads the session and the CSRF token of the caller.</summary>
    public async Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync()
    {
        var response = await Client.GetAsync("/v1/session", CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        using var document = JsonDocument.Parse(body);
        var csrfToken = document.RootElement.TryGetProperty("csrf_token", out var token)
            ? token.GetString() ?? string.Empty
            : string.Empty;

        return (response, csrfToken);
    }

    /// <summary>
    /// Starts a sign in and returns the response of the start command, the
    /// address of the provider and the address of the callback.
    /// </summary>
    public async Task<SignInStart> StartSignInAsync(string actor = "alice")
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/auth/github/start?actor={actor}")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("Origin", Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrfToken);

        var response = await Client.SendAsync(request, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"The sign in could not be started: {response.StatusCode} {body}");
        }

        using var document = JsonDocument.Parse(body);
        var authorizeUrl = document.RootElement.GetProperty("authorize_url").GetString()!;

        var authorize = await Client.GetAsync(new Uri(authorizeUrl), CancellationToken.None);
        var callback = authorize.Headers.Location
            ?? throw new InvalidOperationException("The fixture did not return a callback address.");

        return new SignInStart(response, authorizeUrl, callback);
    }

    /// <summary>Finishes a sign in with the address of the callback.</summary>
    public Task<HttpResponseMessage> CompleteSignInAsync(Uri callback) =>
        Client.GetAsync(callback.PathAndQuery, CancellationToken.None);

    /// <summary>
    /// Signs a seeded account in and returns the response of the callback.
    /// </summary>
    public async Task<HttpResponseMessage> SignInAsync(string actor = "alice")
    {
        var start = await StartSignInAsync(actor);
        return await CompleteSignInAsync(start.Callback);
    }

    /// <summary>Sends an unsafe request with the CSRF token of the session.</summary>
    public async Task<HttpResponseMessage> SendUnsafeAsync(
        HttpMethod method,
        string path,
        string? body,
        string? csrfToken = null)
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

        request.Headers.TryAddWithoutValidation("Origin", Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", token);

        return await Client.SendAsync(request, CancellationToken.None);
    }

    public async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonSerializer.Deserialize<T>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"The response is not the expected JSON: {body}");
    }

    /// <summary>Reads a JSON document of a response.</summary>
    public async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return JsonDocument.Parse(body);
    }

    /// <summary>A context against the isolated database of the test run.</summary>
    public async Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action)
    {
        var options = new DbContextOptionsBuilder<BlindmeshDbContext>()
            .UseNpgsql(Fixture.ConnectionString)
            .Options;

        await using var dbContext = new BlindmeshDbContext(options);
        return await action(dbContext);
    }

    /// <summary>The number of sessions that belong to a signed in contributor.</summary>
    public Task<int> CountSessionsAsync() =>
        WithDatabaseAsync(async dbContext => await dbContext.Sessions.CountAsync(item => item.UserId != null));

    /// <summary>The contributor of a provider subject.</summary>
    public async Task<Blindmesh.Domain.Identity.User?> FindUserBySubjectAsync(string subject)
    {
        var userId = await WithDatabaseAsync(async dbContext =>
        {
            var identity = await dbContext.ExternalIdentities
                .FirstOrDefaultAsync(item => item.Provider == "github" && item.Subject == subject);

            return identity?.UserId;
        });

        if (userId is null)
        {
            return null;
        }

        return await WithDatabaseAsync(async dbContext =>
            await dbContext.Users.FirstOrDefaultAsync(item => item.Id == userId.Value));
    }

    /// <summary>A client that keeps no cookie, like a visitor without a session.</summary>
    public HttpClient CreateClientWithoutCookies() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

/// <summary>The addresses of a started sign in.</summary>
internal sealed record SignInStart(HttpResponseMessage Response, string AuthorizeUrl, Uri Callback);
