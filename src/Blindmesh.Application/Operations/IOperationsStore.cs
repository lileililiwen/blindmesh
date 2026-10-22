using Blindmesh.Domain.Operations;

namespace Blindmesh.Application.Operations;

/// <summary>
/// The persistence of the operations module. The implementation lives in the
/// infrastructure; the application service never touches the database
/// directly.
/// </summary>
public interface IOperationsStore
{
    Task AddBackupAsync(BackupRecord backup, CancellationToken cancellationToken);

    Task<IReadOnlyList<BackupRecord>> ListBackupsAsync(CancellationToken cancellationToken);

    Task<BackupRecord?> FindBackupAsync(Guid id, CancellationToken cancellationToken);

    Task AddRestoreRehearsalAsync(RestoreRehearsal rehearsal, CancellationToken cancellationToken);

    Task<IReadOnlyList<RestoreRehearsal>> ListRestoreRehearsalsAsync(CancellationToken cancellationToken);

    Task<RestoreRehearsal?> FindRestoreRehearsalAsync(Guid id, CancellationToken cancellationToken);

    Task AddDeletionRequestAsync(DeletionRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<DeletionRequest>> ListDeletionRequestsAsync(CancellationToken cancellationToken);
}
