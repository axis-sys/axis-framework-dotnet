using Axis;
using AxisMediator.Contracts;
using Npgsql;

namespace AxisRepository.Postgres;

/// <inheritdoc cref="IPostgresUnitOfWork"/>
public sealed class PostgresUnitOfWork(
    IAxisMediator mediator,
    NpgsqlDataSource dataSource,
    IAxisTelemetry telemetry,
    IAxisLogger<PostgresUnitOfWork> logger,
    IAxisRepositoryOutbox outbox
) : IPostgresUnitOfWork
{
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public bool IsFaulted { get; private set; }
    public bool HasUncommittedWrites { get; private set; }

    public void MarkFaulted() => IsFaulted = true;
    public void MarkWrite() => HasUncommittedWrites = true;

    public async Task<NpgsqlCommand> NewCommandAsync(string sql)
    {
        if (_connection is null || _transaction is null)
        {
            // The Task<NpgsqlCommand> signature cannot carry an AxisResult, so a failed lazy start surfaces as
            // an exception here (which the repository base maps to AxisError) rather than building a command on
            // a null connection — which would otherwise throw an unmapped error on the next execute.
            var start = await StartAsync();
            if (start.IsFailure)
                throw new AxisDbException(start.Errors[0].Code);
        }

        return new(sql, _connection, _transaction);
    }

    public async Task<AxisResult> StartAsync()
    {
        var ct = mediator.CancellationToken;
        using var span = telemetry.StartSpan("db.postgres.connect", AxisSpanKind.Client);
        span.SetTag("db.system", "postgresql");

        try
        {
            _connection ??= await dataSource.OpenConnectionAsync(ct);
            _transaction = await _connection.BeginTransactionAsync(ct);
            HasUncommittedWrites = false;
            span.SetStatus(AxisSpanStatus.Ok);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            logger.LogError(ex, "Failed to open Postgres connection");
            return AxisError.InternalServerError("POSTGRES_ERROR_STARTING_CONNECTION");
        }
    }

    public async Task<AxisResult> SaveChangesAsync()
    {
        if (IsFaulted)
            return AxisError.InternalServerError("POSTGRES_TRANSACTION_FAULTED");

        if (_transaction == null)
            return AxisError.InternalServerError("POSTGRES_TRANSACTION_NOT_STARTED");

        // Drain any queued outbox events into THIS transaction before committing, so business state and the
        // events land atomically. No-op by default; the AxisOutbox adapter supplies the real drain. A drain
        // failure aborts the commit — the uncommitted transaction is rolled back when the unit of work is disposed.
        var drain = await outbox.DrainAsync(_connection!, _transaction, mediator.CancellationToken);
        if (drain.IsFailure)
            return drain;

        using var span = telemetry.StartSpan("db.postgres.commit", AxisSpanKind.Client);
        span.SetTag("db.system", "postgresql");

        try
        {
            await _transaction.CommitAsync(mediator.CancellationToken);
            _transaction = null;
            HasUncommittedWrites = false;
            span.SetStatus(AxisSpanStatus.Ok);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            logger.LogError(ex, "Failed to commit Postgres transaction");
            return AxisError.InternalServerError("POSTGRES_SAVING_CHANGES_ERROR");
        }
    }

    public async Task ReleaseConnectionAsync()
    {
        // Roll back any uncommitted work (reads, or writes not yet saved) and hand the connection back
        // to the pool. The transaction is abandoned deliberately: callers release only after capturing
        // the reads they need and before a long external I/O, then reopen for the durable writes. The
        // connection is disposed in a finally so the pool slot is always returned even if the rollback
        // throws (e.g. cancelled token or an already-broken connection) — leaking it would defeat the
        // whole point of releasing it.
        try
        {
            if (_transaction is not null)
                await _transaction.RollbackAsync(mediator.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to roll back transaction while releasing connection");
        }
        finally
        {
            _transaction = null;
            HasUncommittedWrites = false;
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
                _connection = null;
            }
        }
    }

    public async Task<AxisResult> RollbackAsync()
    {
        using var span = telemetry.StartSpan("db.postgres.rollback", AxisSpanKind.Client);
        span.SetTag("db.system", "postgresql");

        try
        {
            if (_transaction != null) await _transaction.RollbackAsync(mediator.CancellationToken);
            HasUncommittedWrites = false;
            span.SetStatus(AxisSpanStatus.Ok);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            span.RecordException(ex);
            logger.LogError(ex, "Failed to rollback Postgres transaction");
            return AxisError.InternalServerError("POSTGRES_ROLLBACK_ERROR");
        }
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await (_connection?.DisposeAsync() ?? ValueTask.CompletedTask);
    }
}
