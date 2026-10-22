using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Tests.Operations;

/// <summary>
/// The host of the operations tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. Every command of a test goes through
/// the real endpoints, so the authorization of the API is part of the result.
/// </summary>
internal sealed class OperationsTestHost : IDisposable
{
    private readonly IdentityTestHost _host;

    public OperationsTestHost(RuntimeFixture fixture)
    {
        _host = new IdentityTestHost(fixture);
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "erin") => _host.SignInAsync(actor);

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    public Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) =>
        _host.WithDatabaseAsync(async dbContext =>
        {
            await action(dbContext);
            return true;
        });

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    /// <summary>Sends a request with the CSRF token of the session.</summary>
    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return Client.SendAsync(request, CancellationToken.None);
    }

    /// <summary>
    /// Sends a request with the CSRF token of the session. The token is read
    /// from the session, so a command of a test is refused the same way the
    /// browser is refused without the token.
    /// </summary>
    public async Task<HttpResponseMessage> SendWithTokenAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null)
    {
        var (_, csrfToken) = await _host.ReadSessionAsync();

        var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

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
    /// holds a settled financial record.
    /// </summary>
    public async Task ClaimGrantAsync()
    {
        var response = await SendWithTokenAsync(
            HttpMethod.Post,
            "/v1/wallet/grant",
            "{\"rules_accepted\":true}",
            $"operations-grant-{Guid.NewGuid():N}");

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"The grant could not be claimed: {response.StatusCode} {body}");
        }
    }

    /// <summary>The identifier of the account of a username.</summary>
    public Task<Guid?> FindUserIdAsync(string username) =>
        WithDatabaseAsync(async dbContext =>
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Username == username);

            return user?.Id;
        });

    public void Dispose() => _host.Dispose();
}
