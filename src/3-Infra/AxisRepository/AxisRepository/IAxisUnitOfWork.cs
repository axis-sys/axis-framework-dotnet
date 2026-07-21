namespace Axis;

/// <summary>
/// The transactional unit of work over an ADO.NET connection: begin (<see cref="StartAsync"/>), commit
/// (<see cref="SaveChangesAsync"/>) or roll back (<see cref="RollbackAsync"/>) a transaction; <c>InTransactionAsync</c>
/// wraps the railway commit/rollback. Each dialect adapter supplies one implementation.
/// </summary>
public interface IAxisUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>Opens a connection and begins a transaction (lazily; idempotent if already started).</summary>
    Task<AxisResult> StartAsync();

    /// <summary>Commits the current transaction; fails fast if the unit of work is faulted or none was started.</summary>
    Task<AxisResult> SaveChangesAsync();

    /// <summary>Rolls back the current transaction, if any.</summary>
    Task<AxisResult> RollbackAsync();

    /// <summary>
    /// Returns the currently held connection (rolling back any uncommitted work) to the pool, so a
    /// slow external call in the middle of a unit of work does not pin a pooled connection — and an
    /// open transaction — idle across it. The next command transparently reopens a fresh connection
    /// and transaction. This member has no default implementation and every implementer must supply
    /// one; implementations that do not pool a connection simply return <see cref="Task.CompletedTask"/>.
    /// </summary>
    Task ReleaseConnectionAsync();

    /// <summary>
    /// Executes <paramref name="work"/> inside a transaction, commits on success and
    /// rolls back on failure or exception. Exceptions are re-thrown after rollback.
    /// </summary>
    Task<AxisResult> InTransactionAsync(Func<Task<AxisResult>> work)
        => StartAsync().ThenAsync(() => CommitOrRollbackAsync(work));

    /// <summary>
    /// Executes <paramref name="work"/> inside a transaction, commits on success and
    /// rolls back on failure or exception. Exceptions are re-thrown after rollback.
    /// </summary>
    Task<AxisResult<T>> InTransactionAsync<T>(Func<Task<AxisResult<T>>> work)
        => StartAsync().ThenAsync(() => CommitOrRollbackAsync(work));

    // The scope inside a successfully started transaction: roll back on a failed result (a valueless Then
    // preserves the result's value across the commit), and roll back + re-throw on an exception. The only
    // try/catch is this exceptional-cleanup boundary; success/failure flow is the railway chain, not if/else.
    private async Task<AxisResult> CommitOrRollbackAsync(Func<Task<AxisResult>> work)
    {
        try
        {
            return await work()
                .TapErrorAsync(_ => RollbackAsync())
                .ThenAsync(SaveChangesAsync);
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }

    private async Task<AxisResult<T>> CommitOrRollbackAsync<T>(Func<Task<AxisResult<T>>> work)
    {
        try
        {
            return await work()
                .TapErrorAsync(_ => RollbackAsync())
                .ThenAsync(_ => SaveChangesAsync());
        }
        catch
        {
            await RollbackAsync();
            throw;
        }
    }
}
