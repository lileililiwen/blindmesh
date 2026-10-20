using System.Net;
using System.Text;
using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Identity;
using Blindmesh.Tests.Runtime;

namespace Blindmesh.Tests.Credits;

/// <summary>
/// The host of the credit tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would.
/// </summary>
internal sealed class CreditTestHost : IDisposable
{
    private readonly IdentityTestHost _host;

    public CreditTestHost(RuntimeFixture fixture)
    {
        _host = new IdentityTestHost(fixture);
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync() => _host.ReadSessionAsync();

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    /// <summary>Reads the JSON document of a response.</summary>
    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    /// <summary>
    /// Sends the grant command with the CSRF token of the session and with the
    /// idempotency key of the command.
    /// </summary>
    public async Task<HttpResponseMessage> SendGrantAsync(string? idempotencyKey, string body)
    {
        var (_, csrfToken) = await ReadSessionAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/wallet/grant")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        request.Headers.TryAddWithoutValidation("Origin", IdentityTestHost.Origin);
        request.Headers.TryAddWithoutValidation("X-CSRF-Token", csrfToken);

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        return await Client.SendAsync(request, CancellationToken.None);
    }

    /// <summary>Reads the wallet of the signed in contributor.</summary>
    public Task<HttpResponseMessage> GetWalletAsync() => Client.GetAsync("/v1/wallet", CancellationToken.None);

    /// <summary>Reads the history of the wallet of the signed in contributor.</summary>
    public Task<HttpResponseMessage> GetHistoryAsync() => Client.GetAsync("/v1/wallet/transactions", CancellationToken.None);

    public void Dispose() => _host.Dispose();
}
