using System.Data.Common;

namespace Axis;

/// <summary>
/// A unit of work backed by an ADO.NET provider, parameterized by the provider's command type. This is
/// the dialect seam consumed by <see cref="AxisRepositoryBase{TCommand,TReader,TParameters}"/>: each
/// database (Postgres via Npgsql, MySQL via MySqlConnector, …) provides one implementation, and the
/// shared repository base never names a concrete provider type.
/// </summary>
public interface IDbUnitOfWork<TCommand> : IAxisUnitOfWork where TCommand : DbCommand
{
    /// <summary>Creates a command bound to this unit of work's connection and transaction (opening them lazily).</summary>
    Task<TCommand> NewCommandAsync(string sql);

    /// <summary>
    /// True once a command on this unit of work has errored. Some engines (Postgres, for example) abort the whole
    /// transaction on any error, so every later command would fail and a commit would silently roll
    /// back. The repository base sets this and refuses further work so the failure surfaces cleanly
    /// instead of cascading or losing data.
    /// </summary>
    bool IsFaulted { get; }

    /// <summary>Marks the transaction as aborted; called by the repository base after a command error.</summary>
    void MarkFaulted();

    /// <summary>
    /// True once a write has executed successfully in the current (uncommitted) transaction. The repository
    /// base uses it to decide whether a transient failure is safe to retry on a fresh connection (no write
    /// yet) or must be surfaced so the caller replays the whole unit of work — retrying in place would hit a
    /// "transaction aborted" error and lose the earlier write.
    /// </summary>
    bool HasUncommittedWrites { get; }

    /// <summary>Records that a write executed in the current transaction; called by the repository base after a non-query.</summary>
    void MarkWrite();
}
