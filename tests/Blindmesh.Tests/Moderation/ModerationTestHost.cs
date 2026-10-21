using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Findings;
using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Moderation;

/// <summary>
/// The host of the moderation tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. Every command of a test goes through
/// the real endpoints, so the transaction, the escrow and the idempotency of
/// the API are part of the result.
/// </summary>
internal sealed class ModerationTestHost : IDisposable
{
    private readonly FindingsTestHost _host;

    public ModerationTestHost(RuntimeFixture fixture)
    {
        _host = new FindingsTestHost(fixture);
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync() => _host.ReadSessionAsync();

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    public Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) => _host.WithDatabaseAsync(action);

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    /// <summary>Sends a request with the CSRF token of the session.</summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null) =>
        _host.SendAsync(method, path, body, idempotencyKey);

    /// <summary>
    /// Sends a request with the CSRF token of the session and with an explicit
    /// idempotency key.
    /// </summary>
    public Task<HttpResponseMessage> SendWithTokenAsync(
        HttpMethod method,
        string path,
        string? body,
        string? idempotencyKey,
        string? csrfToken) =>
        _host.SendWithTokenAsync(method, path, body, idempotencyKey, csrfToken);

    public void Dispose() => _host.Dispose();
}
