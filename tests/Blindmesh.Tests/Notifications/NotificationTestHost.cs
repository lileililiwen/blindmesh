using System.Text.Json;
using Blindmesh.Domain.Notifications;
using Blindmesh.Infrastructure.Persistence;
using Blindmesh.Tests.Findings;
using Blindmesh.Tests.Runtime;
using Blindmesh.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Tests.Notifications;

/// <summary>
/// The host of the notification tests. It runs the real API against the
/// isolated PostgreSQL of the test run and drives the sign in through the
/// local OAuth fixture, exactly like a browser would. The real consumer of the
/// outbox and the real sender of the notifications run inside the host, so a
/// notification of a test is written and sent by the same code the product
/// runs and not by a stand in.
/// </summary>
internal sealed class NotificationTestHost : IDisposable
{
    private readonly FindingsTestHost _host;

    /// <summary>
    /// The lines the worker of the test run wrote. The provider is registered
    /// in the container of the API, so the worker logs into it.
    /// </summary>
    public CapturingLoggerProvider Logger { get; } = new();

    /// <summary>
    /// The host of the tests. The port of the mail service is the port of the
    /// sink of the test; the real sink of the test run is used when the port
    /// is the port of the mail sink of the environment.
    /// </summary>
    public NotificationTestHost(RuntimeFixture fixture, int mailPort)
    {
        _host = new FindingsTestHost(
            fixture,
            configureServices: services =>
            {
                // The handlers of the notification module are scoped, so they
                // share the scoped database context and the transaction of the
                // cycle that claimed the message. The registration is the one of
                // the worker.
                services.AddScoped<IOutboxHandler, ApplicationReceivedNotificationHandler>();
                services.AddScoped<IOutboxHandler, ApplicationAcceptedNotificationHandler>();
                services.AddScoped<IOutboxHandler, SubmissionReceivedNotificationHandler>();
                services.AddScoped<IOutboxHandler, CorrectionRequestedNotificationHandler>();
                services.AddScoped<IOutboxHandler, ParticipationAcceptedNotificationHandler>();
                services.AddScoped<IOutboxHandler, FindingConfirmedNotificationHandler>();
                services.AddScoped<IOutboxHandler, DisputeOpenedNotificationHandler>();
                services.AddScoped<IOutboxHandler, DisputeResolvedNotificationHandler>();
                services.AddScoped<IOutboxHandler, DisputeAppealedNotificationHandler>();
                services.AddScoped<IOutboxHandler, ArbitrationCompensatedNotificationHandler>();

                services.AddSingleton<ILoggerProvider>(Logger);

                services.AddHostedService<OutboxClaimService>();
                services.AddHostedService<NotificationDeliveryService>();
            },
            overrides: new Dictionary<string, string?>
            {
                ["Blindmesh:Mail:Host"] = "127.0.0.1",
                ["Blindmesh:Mail:Port"] = mailPort.ToString(),
            });
    }

    public HttpClient Client => _host.Client;

    public Task<HttpResponseMessage> SignInAsync(string actor = "alice") => _host.SignInAsync(actor);

    public Task<(HttpResponseMessage Response, string CsrfToken)> ReadSessionAsync() => _host.ReadSessionAsync();

    public Task<T> WithDatabaseAsync<T>(Func<BlindmeshDbContext, Task<T>> action) => _host.WithDatabaseAsync(action);

    public Task WithDatabaseAsync(Func<BlindmeshDbContext, Task> action) => _host.WithDatabaseAsync(action);

    public Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) => _host.ReadJsonAsync(response);

    public HttpClient CreateClientWithoutCookies() => _host.CreateClientWithoutCookies();

    public Task ClaimGrantAsync() => _host.ClaimGrantAsync();

    public Task<string> CreateActiveProjectAsync(string name = "Notification project") =>
        _host.CreateActiveProjectAsync(name);

    public Task OfferCapabilityAsync(string kind = "region", string value = "eu-central") =>
        _host.OfferCapabilityAsync(kind, value);

    public Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string? body = null,
        string? idempotencyKey = null) =>
        _host.SendAsync(method, path, body, idempotencyKey);

    /// <summary>
    /// Moves the moment of the next attempt of the deliveries into the past, so
    /// a test does not wait for the wall clock. The schedule itself is proved
    /// by the boundary test, which reads the moments the service computes.
    /// </summary>
    public async Task MakeDeliveriesDueAsync()
    {
        await WithDatabaseAsync(async dbContext =>
        {
            var now = DateTimeOffset.UtcNow;

            var deliveries = await dbContext.NotificationDeliveries
                .Where(delivery => delivery.Status == NotificationStatus.Pending || delivery.Status == NotificationStatus.Failed)
                .ToListAsync();

            foreach (var delivery in deliveries)
            {
                delivery.NextAttemptAt = now.AddMinutes(-1);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Waits until the list of the notifications of the caller holds the wanted
    /// state. The consumer of the outbox runs in the background, so a
    /// notification arrives a moment after the command.
    /// </summary>
    public async Task WaitForNotificationsAsync(
        Func<IReadOnlyList<NotificationDelivery>, bool> ready,
        string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(60);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var rows = await WithDatabaseAsync(async dbContext =>
                (IReadOnlyList<NotificationDelivery>)await dbContext.NotificationDeliveries
                    .AsNoTracking()
                    .ToListAsync());

            if (ready(rows))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException($"The notifications never reached the state of {what}.");
    }

    /// <summary>
    /// Waits until the delivery of one identity holds the wanted state.
    /// </summary>
    public async Task<NotificationDelivery> WaitForDeliveryAsync(
        Func<NotificationDelivery, bool> ready,
        string what)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var rows = await WithDatabaseAsync(async dbContext =>
                (IReadOnlyList<NotificationDelivery>)await dbContext.NotificationDeliveries
                    .AsNoTracking()
                    .ToListAsync());

            var match = rows.FirstOrDefault(ready);

            if (match is not null)
            {
                return match;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException($"The delivery never reached the state of {what}.");
    }

    public void Dispose() => _host.Dispose();
}
