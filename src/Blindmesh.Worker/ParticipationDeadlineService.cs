using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Worker;

/// <summary>
/// Sweeps the deadlines of the seats of the tasks. The sweep expires the
/// applications that wait longer than 48 hours for a decision and the seats
/// that wait longer than 24 hours for the start. It calls the same
/// application method as the API and uses the injected clock, so a deadline
/// never depends on the wall clock of the host.
/// </summary>
public sealed class ParticipationDeadlineService(
    IServiceScopeFactory scopeFactory,
    ILogger<ParticipationDeadlineService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The participation deadline cycle failed.");
            }

            try
            {
                await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var provider = scope.ServiceProvider;

        var participations = provider.GetRequiredService<ParticipationService>();
        var transactionFactory = provider.GetRequiredService<ICommandTransactionFactory>();

        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var report = await participations.ExpireOverdueAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (report.Expired > 0)
        {
            logger.LogInformation(
                "The participation deadline sweep expired {Expired} seats and released {Released} of them.",
                report.Expired,
                report.Released);
        }
    }
}
