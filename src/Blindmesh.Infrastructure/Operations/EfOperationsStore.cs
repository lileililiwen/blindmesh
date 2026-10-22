using Blindmesh.Application.Operations;
using Blindmesh.Domain.Operations;
using Blindmesh.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Blindmesh.Infrastructure.Operations;

/// <summary>
/// The persistence of the operations module. Every write is a single statement
/// of the scoped context of the caller, so a failed command never leaves a
/// partial record behind.
/// </summary>
public sealed class EfOperationsStore(BlindmeshDbContext dbContext) : IOperationsStore
{
    public async Task AddBackupAsync(BackupRecord backup, CancellationToken cancellationToken)
    {
        dbContext.Set<BackupRecord>().Add(backup);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<BackupRecord>()
            .AsNoTracking()
            .OrderByDescending(backup => backup.TakenAt)
            .ToListAsync(cancellationToken);

    public Task<BackupRecord?> FindBackupAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<BackupRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(backup => backup.Id == id, cancellationToken);

    public async Task AddRestoreRehearsalAsync(RestoreRehearsal rehearsal, CancellationToken cancellationToken)
    {
        dbContext.Set<RestoreRehearsal>().Add(rehearsal);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RestoreRehearsal>> ListRestoreRehearsalsAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<RestoreRehearsal>()
            .AsNoTracking()
            .OrderByDescending(rehearsal => rehearsal.StartedAt)
            .ToListAsync(cancellationToken);

    public Task<RestoreRehearsal?> FindRestoreRehearsalAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Set<RestoreRehearsal>()
            .AsNoTracking()
            .FirstOrDefaultAsync(rehearsal => rehearsal.Id == id, cancellationToken);

    public async Task AddDeletionRequestAsync(DeletionRequest request, CancellationToken cancellationToken)
    {
        dbContext.Set<DeletionRequest>().Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeletionRequest>> ListDeletionRequestsAsync(CancellationToken cancellationToken) =>
        await dbContext.Set<DeletionRequest>()
            .AsNoTracking()
            .OrderByDescending(request => request.ReceivedAt)
            .ToListAsync(cancellationToken);
}
