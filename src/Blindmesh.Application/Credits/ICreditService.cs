namespace Blindmesh.Application.Credits;

/// <summary>
/// The credit service of the product. It owns the journal of the credits, the
/// lots with their expiry and the limited grant. The escrow primitives are
/// internal: they are called by the package that owns the task, inside the
/// transaction of that caller. There is no public transfer and no public
/// balance editor.
/// </summary>
public interface ICreditService
{
    /// <summary>
    /// The wallet of a contributor. A wallet of another contributor is not
    /// found and never carries an amount.
    /// </summary>
    Task<CreditWalletView?> GetWalletAsync(
        CreditActor actor,
        Guid ownerId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The transactions of the wallet of a contributor, in the stable
    /// descending order of the list envelope.
    /// </summary>
    Task<CreditTransactionPage?> ListTransactionsAsync(
        CreditActor actor,
        Guid ownerId,
        int limit,
        CreditCursor? cursor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Claims the limited grant of the contributor. The grant is issued once
    /// per provider subject, it obeys the daily limit of the platform and it
    /// expires after thirty days.
    /// </summary>
    Task<CreditCommandResult> GrantOnceAsync(
        CreditActor actor,
        CreditGrantCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reserves the credits of the actor for a task. The credits leave the
    /// ordinary bucket and wait in the reserved bucket of the task. The
    /// ordinary bucket never goes negative.
    /// </summary>
    Task<CreditCommandResult> ReserveTaskAsync(
        CreditActor actor,
        CreditReserveCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the reserved credits of a task into the pending bucket of the
    /// contributor that started the work.
    /// </summary>
    Task<CreditCommandResult> BeginPendingAsync(
        CreditActor actor,
        CreditPendingCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Pays the pending credits of the actor out. A grant that expired while
    /// it was reserved becomes an earned credit that never expires.
    /// </summary>
    Task<CreditCommandResult> SettleAsync(
        CreditActor actor,
        CreditSettleCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns the reserved credits of a task. An expired grant goes to the
    /// expiry sink instead of back to the publisher.
    /// </summary>
    Task<CreditCommandResult> RefundUnstartedAsync(
        CreditActor actor,
        CreditRefundCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the pending credits of a disputed seat to the contributor that
    /// did the work, or back to the publisher that owns the task. The reserved
    /// escrow of the publisher is never touched. The command is internal: it is
    /// called by the package that owns the dispute, inside the transaction of
    /// that caller.
    /// </summary>
    Task<CreditCommandResult> ArbitrateAsync(
        CreditActor actor,
        CreditArbitrationCommand command,
        CancellationToken cancellationToken);

    /// <summary>
    /// Compensates an arbitration after an appeal. It writes new, balanced
    /// entries that move the amount back. It never touches an entry of an
    /// earlier transaction.
    /// </summary>
    Task<CreditCommandResult> CompensateArbitrationAsync(
        CreditActor actor,
        CreditCompensationCommand command,
        CancellationToken cancellationToken);
}
