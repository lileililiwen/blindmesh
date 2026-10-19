using Blindmesh.Application.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Blindmesh.Infrastructure.Persistence;

/// <summary>
/// Opens the PostgreSQL transaction that carries the idempotency record, the
/// domain effects and the outbox message of one command.
/// </summary>
public sealed class EfCommandTransactionFactory(BlindmeshDbContext dbContext) : ICommandTransactionFactory
{
    public async Task<ICommandTransactionScope> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new EfCommandTransactionScope(dbContext, transaction);
    }
}

internal sealed class EfCommandTransactionScope(
    BlindmeshDbContext dbContext,
    IDbContextTransaction transaction) : ICommandTransactionScope
{
    private bool _committed;

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _committed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_committed)
        {
            await transaction.RollbackAsync();
        }

        await transaction.DisposeAsync();
    }
}
