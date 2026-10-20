using Blindmesh.Application.Abstractions;
using Blindmesh.Application.Findings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Blindmesh.Worker;

/// <summary>
/// Sweeps the deadlines of the review of the evidence. The sweep accepts the
/// base of a seat that waited longer than 72 hours for the review of the owner
/// and it opens a dispute for a correction that was not sent in time. It calls
/// the same application method as the API and it uses the injected clock, so a
/// deadline never depends on the wall clock of the host. The sweep never
/// resolves a dispute: the arbitration is another package.
/// </summary>
public sealed class ReviewDeadlineService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReviewDeadlineService> logger) : BackgroundService
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
                logger.LogError(exception, "The review deadline cycle failed.");
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

        var findings = provider.GetRequiredService<FindingsService>();
        var transactionFactory = provider.GetRequiredService<ICommandTransactionFactory>();

        await using var transaction = await transactionFactory.BeginAsync(cancellationToken);

        var report = await findings.SweepOverdueAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        if (report.Accepted > 0 || report.Disputed > 0)
        {
            logger.LogInformation(
                "The review deadline sweep accepted {Accepted} seats and opened {Disputed} disputes.",
                report.Accepted,
                report.Disputed);
        }
    }
}
