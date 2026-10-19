namespace Blindmesh.Application.Abstractions;

/// <summary>
/// Opens the command transaction shared by the idempotency record, the domain
/// effects and the outbox message of one command.
/// </summary>
public interface ICommandTransactionFactory
{
    Task<ICommandTransactionScope> BeginAsync(CancellationToken cancellationToken);
}

public interface ICommandTransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
}
