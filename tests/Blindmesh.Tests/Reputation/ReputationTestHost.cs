using System.Text.Json;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Findings;
using Blindmesh.Tests.Runtime;
using Blindmesh.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Blindmesh.Tests.Reputation;

/// <summary>
/// The host of the reputation tests. It runs the real API against the isolated
/// PostgreSQL of the test run and drives the sign in through the local OAuth
/// fixture, exactly like a browser would. The real consumer of the outbox runs
/// inside the host, so the reputation of a test is applied by the same worker
/// code the product runs and not by a stand in.
/// </summary>
internal sealed class ReputationTestHost : IDisposable
{
    private readonly FindingsTestHost _host;

    public ReputationTestHost(RuntimeFixture fixture)
    {
        _host = new FindingsTestHost(fixture, services =>
        {
            // The handlers of the reputation module are scoped, so they share
            // the scoped database context and the transaction of the cycle that
            // claimed the message. The registration is the one of the worker.
            services.AddScoped<IOutboxHandler, ParticipationAcceptedReputationHandler>();
            services.AddScoped<IOutboxHandler, FindingConfirmedReputationHandler>();
            services.AddScoped<IOutboxHandler, ArbitrationReversedReputationHandler>();

            services.AddHostedService<OutboxClaimService>();
        });
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync() => _host.ReadSessionAsync();

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    public Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) => _host.WithDatabaseAsync(action);

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    /// <summary>Claims the limited grant of the signed in contributor.</summary>
    public Task ClaimGrantAsync() => _host.ClaimGrantAsync();

    /// <summary>Creates a public project of the signed in contributor and activates it.</summary>
    public Task<string> CreateActiveProjectAsync(string name = "Reputation project") =>
        _host.CreateActiveProjectAsync(name);

    /// <summary>Offers a public capability of the signed in contributor.</summary>
    public Task OfferCapabilityAsync(string kind = "region", string value = "eu-central") =>
        _host.OfferCapabilityAsync(kind, value);

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

    /// <summary>
    /// Waits until the lines of the reputation of a contributor hold the state
    /// the test waits for. The consumer of the outbox runs in the background,
    /// so the effect of an event arrives a moment after the command.
    /// </summary>
    public async Task WaitForReputationAsync(
        Guid userId,
        Func<int, bool> ready,
        string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var count = await WithDatabaseAsync(async dbContext =>
                await dbContext.ReputationEvents.AsNoTracking()
                    .CountAsync(reputationEvent => reputationEvent.UserId == userId));

            if (ready(count))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException($"The reputation of the contributor never reached the state of {what}.");
    }

    /// <summary>
    /// Waits until the consumer of the outbox has taken the message a second
    /// time, so a redelivery of the same message is proven.
    /// </summary>
    public async Task WaitForRedeliveryAsync(Guid messageId, int attempts)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var taken = await WithDatabaseAsync(async dbContext =>
                await dbContext.OutboxMessages.AsNoTracking()
                    .Where(message => message.Id == messageId)
                    .Select(message => message.Attempts)
                    .FirstOrDefaultAsync());

            if (taken >= attempts)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException($"The outbox message {messageId} was never taken {attempts} times.");
    }

    public void Dispose() => _host.Dispose();
}
