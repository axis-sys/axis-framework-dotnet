using Axis;
using AxisMediator.Contracts;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <inheritdoc cref="IMySqlUnitOfWork"/>
public sealed class MySqlUnitOfWork(
    IAxisMediator mediator,
    MySqlDataSource dataSource,
    IAxisTelemetry telemetry,
    IAxisLogger<MySqlUnitOfWork> logger,
    IAxisRepositoryOutbox outbox
) : IMySqlUnitOfWork
{
    private MySqlConnection? _connection;
    private MySqlTransaction? _transaction;

    public bool IsFaulted { get; private set; }
    public bool HasUncommittedWrites { get; private set; }

    public void MarkFaulted() => IsFaulted = true;
    public void MarkWrite() => HasUncommittedWrites = true;

    public async Task<MySqlCommand> NewCommandAsync(string sql)
    {
        if (_connection is null || _transaction is null)
        {
            // The Task<MySqlCommand> signature cannot carry an AxisResult, so a failed lazy start surfaces as
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
        using var span = telemetry.StartSpan("db.mysql.connect", AxisSpanKind.Client);
        span.SetTag("db.system", "mysql");

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
            logger.LogError(ex, "Failed to open MySQL connection");
            return AxisError.InternalServerError("MYSQL_ERROR_STARTING_CONNECTION");
        }
    }

    public async Task<AxisResult> SaveChangesAsync()
    {
        if (IsFaulted)
            return AxisError.InternalServerError("MYSQL_TRANSACTION_FAULTED");

        if (_transaction == null)
            return AxisError.InternalServerError("MYSQL_TRANSACTION_NOT_STARTED");

        // Drain any queued outbox events into THIS transaction before committing, so business state and the
        // events land atomically. No-op by default; the AxisOutbox adapter supplies the real drain. A drain
        // failure aborts the commit — the uncommitted transaction is rolled back when the unit of work is disposed.
        var drain = await outbox.DrainAsync(_connection!, _transaction, mediator.CancellationToken);
        if (drain.IsFailure)
            return drain;

        using var span = telemetry.StartSpan("db.mysql.commit", AxisSpanKind.Client);
        span.SetTag("db.system", "mysql");

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
            logger.LogError(ex, "Failed to commit MySQL transaction");
            return AxisError.InternalServerError("MYSQL_SAVING_CHANGES_ERROR");
        }
    }

    public async Task ReleaseConnectionAsync()
    {
        // Roll back any uncommitted work and hand the connection back to the pool, so a slow external
        // call in the middle of a unit of work does not pin a pooled connection and an open transaction
        // idle across it. Disposed in a finally so the pool slot is always returned even if rollback throws.
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
        using var span = telemetry.StartSpan("db.mysql.rollback", AxisSpanKind.Client);
        span.SetTag("db.system", "mysql");

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
            logger.LogError(ex, "Failed to rollback MySQL transaction");
            return AxisError.InternalServerError("MYSQL_ROLLBACK_ERROR");
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
