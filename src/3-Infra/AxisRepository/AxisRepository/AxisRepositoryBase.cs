using System.Data.Common;
using AxisMediator.Contracts;

namespace Axis;

/// <inheritdoc cref="IAxisDbRepository"/>
public abstract class AxisRepositoryBase<TCommand, TReader, TParameters>(
    IAxisMediator mediator,
    IDbUnitOfWork<TCommand> uow
) : IAxisDbRepository where TCommand : DbCommand where TReader : DbDataReader where TParameters : DbParameterCollection
{
    /// <summary>The ambient cancellation token from the mediator, threaded into every command.</summary>
    protected CancellationToken CancellationToken => mediator.CancellationToken;

    /// <summary>Dialect hook: is this a transient, retryable failure (deadlock, serialization, connection)?</summary>
    protected abstract bool IsTransient(DbException exception);

    /// <summary>Dialect hook: is this a unique-constraint violation?</summary>
    protected abstract bool IsDuplicateKey(DbException exception);

    /// <summary>Dialect hook: is the relation/schema missing (not yet created by migrations)?</summary>
    protected abstract bool IsSchemaMissing(DbException exception);

    /// <summary>Prefix for the dialect's diagnostic error codes, e.g. <c>POSTGRES</c> / <c>MYSQL</c>.</summary>
    protected abstract string ErrorPrefix { get; }

    /// <summary>Dialect hook: log a failure through the concrete repository's typed logger.</summary>
    protected abstract void LogError(Exception exception, string message);

    // ─────────────────────────── shared core ───────────────────────────

    private async Task WithRetryAsync(Func<Task> operation, CancellationToken ct)
    {
        int[] delays = [100, 200, 400, 1000];
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await operation();
                return;
            }
            // Retry a transient only while the transaction holds NO durable write. A transient (deadlock,
            // serialization, dropped connection) aborts the whole transaction, so retrying the command in
            // place would hit a "transaction aborted" error and an earlier write would be lost. With no write
            // yet, reset to a fresh connection + transaction and retry; once a write has landed, the transient
            // is surfaced so the caller replays the entire unit of work (the saga resumer does exactly that).
            catch (DbException ex) when (IsTransient(ex) && attempt < delays.Length && !uow.HasUncommittedWrites)
            {
                await uow.ReleaseConnectionAsync();
                await Task.Delay(delays[attempt], ct);
            }
        }
    }

    // A missing relation/schema is an EXPECTED state before migrations run (schemas are created on
    // demand, e.g. via a dev-ops endpoint), so it maps to a transient ServiceUnavailable without an
    // error log: pollers would otherwise turn every pre-migration pass into startup noise. Callers
    // decide how to wait; a genuinely misspelled table shows up as this same typed error on the
    // result path instead of a 500.
    private AxisError SchemaNotReady()
    {
        uow.MarkFaulted();
        return AxisError.ServiceUnavailable($"{ErrorPrefix}_SCHEMA_NOT_READY");
    }

    private async Task<AxisResult> ExecuteCoreAsync(string sql, Action<DbCommand> prepare, string? duplicateKeyCode)
    {
        if (uow.IsFaulted)
            return AxisError.InternalServerError($"{ErrorPrefix}_TRANSACTION_FAULTED");

        try
        {
            await WithRetryAsync(async () =>
            {
                await using var command = await uow.NewCommandAsync(sql);
                prepare(command);
                await command.ExecuteNonQueryAsync(CancellationToken);
                uow.MarkWrite();
            }, CancellationToken);
            return AxisResult.Ok();
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException ex) when (IsDuplicateKey(ex))
        {
            uow.MarkFaulted();
            return AxisError.Conflict(duplicateKeyCode ?? $"{ErrorPrefix}_DUPLICATE_KEY_ERROR");
        }
        catch (DbException ex) when (IsSchemaMissing(ex))
        {
            return SchemaNotReady();
        }
        catch (DbException ex) when (IsTransient(ex))
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_TRANSIENT_ERROR");
            return AxisError.ServiceUnavailable($"{ErrorPrefix}_TRANSIENT_ERROR");
        }
        catch (DbException ex)
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_EXECUTION_ERROR");
            return AxisError.InternalServerError($"{ErrorPrefix}_EXECUTION_ERROR");
        }
    }

    private async Task<AxisResult<int>> ExecuteCountCoreAsync(string sql, Action<DbCommand> prepare, string? duplicateKeyCode)
    {
        if (uow.IsFaulted)
            return AxisError.InternalServerError($"{ErrorPrefix}_TRANSACTION_FAULTED");

        var affected = 0;
        try
        {
            await WithRetryAsync(async () =>
            {
                await using var command = await uow.NewCommandAsync(sql);
                prepare(command);
                affected = await command.ExecuteNonQueryAsync(CancellationToken);
                uow.MarkWrite();
            }, CancellationToken);
            return AxisResult.Ok(affected);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException ex) when (IsDuplicateKey(ex))
        {
            uow.MarkFaulted();
            return AxisError.Conflict(duplicateKeyCode ?? $"{ErrorPrefix}_DUPLICATE_KEY_ERROR");
        }
        catch (DbException ex) when (IsSchemaMissing(ex))
        {
            return SchemaNotReady();
        }
        catch (DbException ex) when (IsTransient(ex))
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_TRANSIENT_ERROR");
            return AxisError.ServiceUnavailable($"{ErrorPrefix}_TRANSIENT_ERROR");
        }
        catch (DbException ex)
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_EXECUTION_ERROR");
            return AxisError.InternalServerError($"{ErrorPrefix}_EXECUTION_ERROR");
        }
    }

    private async Task<AxisResult<T>> GetCoreAsync<T>(string sql, Action<DbCommand> prepare, Func<DbDataReader, T> map, string notFoundCode)
    {
        if (uow.IsFaulted)
            return AxisError.InternalServerError($"{ErrorPrefix}_TRANSACTION_FAULTED");

        T? result = default;
        var found = false;
        try
        {
            await WithRetryAsync(async () =>
            {
                await using var command = await uow.NewCommandAsync(sql);
                prepare(command);
                await using var reader = await command.ExecuteReaderAsync(CancellationToken);
                found = await reader.ReadAsync(CancellationToken);
                if (found) result = map(reader);
            }, CancellationToken);
            return found ? AxisResult.Ok(result!) : AxisError.NotFound(notFoundCode);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException ex) when (IsSchemaMissing(ex))
        {
            return SchemaNotReady();
        }
        catch (DbException ex) when (IsTransient(ex))
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_TRANSIENT_ERROR");
            return AxisError.ServiceUnavailable($"{ErrorPrefix}_TRANSIENT_ERROR");
        }
        catch (DbException ex)
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_GET_ERROR");
            return AxisError.InternalServerError($"{ErrorPrefix}_GET_ERROR");
        }
    }

    private async Task<AxisResult<IEnumerable<T>>> ListCoreAsync<T>(string sql, Action<DbCommand> prepare, Func<DbDataReader, T> map)
    {
        if (uow.IsFaulted)
            return AxisError.InternalServerError($"{ErrorPrefix}_TRANSACTION_FAULTED");

        var list = new List<T>();
        try
        {
            await WithRetryAsync(async () =>
            {
                list.Clear();
                await using var command = await uow.NewCommandAsync(sql);
                prepare(command);
                await using var reader = await command.ExecuteReaderAsync(CancellationToken);
                while (await reader.ReadAsync(CancellationToken))
                    list.Add(map(reader));
            }, CancellationToken);
            return AxisResult.Ok<IEnumerable<T>>(list);
        }
        catch (OperationCanceledException) { throw; }
        catch (DbException ex) when (IsSchemaMissing(ex))
        {
            return SchemaNotReady();
        }
        catch (DbException ex) when (IsTransient(ex))
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_TRANSIENT_ERROR");
            return AxisError.ServiceUnavailable($"{ErrorPrefix}_TRANSIENT_ERROR");
        }
        catch (DbException ex)
        {
            uow.MarkFaulted();
            LogError(ex, $"{ErrorPrefix}_LIST_ERROR");
            return AxisError.InternalServerError($"{ErrorPrefix}_LIST_ERROR");
        }
    }

    // ─────────────────────────── provider-typed surface (inheritance) ───────────────────────────

    /// <summary>Runs a non-query with provider-typed parameter binding, through the shared retry/fault machinery.</summary>
    protected Task<AxisResult> ExecuteAsync(string sql, Action<TParameters> addParams, string? duplicateKeyCode = null) =>
        ExecuteCoreAsync(sql, cmd => addParams((TParameters)cmd.Parameters), duplicateKeyCode);

    /// <summary>Runs a non-query and returns the rows-affected count, through the shared retry/fault machinery.</summary>
    protected Task<AxisResult<int>> ExecuteCountAsync(string sql, Action<TParameters> addParams, string? duplicateKeyCode = null) =>
        ExecuteCountCoreAsync(sql, cmd => addParams((TParameters)cmd.Parameters), duplicateKeyCode);

    /// <summary>Runs a query and maps the first row, or returns <paramref name="notFoundCode"/> when empty.</summary>
    protected Task<AxisResult<T>> GetAsync<T>(string sql, Action<TParameters> addParams, Func<TReader, T> map, string notFoundCode) =>
        GetCoreAsync(sql, cmd => addParams((TParameters)cmd.Parameters), r => map((TReader)r), notFoundCode);

    /// <summary>Runs a query and maps every row into the result sequence.</summary>
    protected Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Action<TParameters> addParams, Func<TReader, T> map) =>
        ListCoreAsync(sql, cmd => addParams((TParameters)cmd.Parameters), r => map((TReader)r));

    protected Task<AxisResult> ExecuteAsync(string sql, string? duplicateKeyCode = null) =>
        ExecuteCoreAsync(sql, static _ => { }, duplicateKeyCode);

    protected Task<AxisResult<T>> GetAsync<T>(string sql, Func<TReader, T> map, string notFoundCode) =>
        GetCoreAsync(sql, static _ => { }, r => map((TReader)r), notFoundCode);

    protected Task<AxisResult<IEnumerable<T>>> ListAsync<T>(string sql, Func<TReader, T> map) =>
        ListCoreAsync(sql, static _ => { }, r => map((TReader)r));

    // ─────────────────────────── binder surface (composition / IAxisDbRepository) ───────────────────────────

    Task<AxisResult> IAxisDbRepository.ExecuteAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode) =>
        ExecuteCoreAsync(sql, cmd => bind(new CommandBinder(cmd)), duplicateKeyCode);

    Task<AxisResult<int>> IAxisDbRepository.ExecuteCountAsync(string sql, Action<IDbParamBinder> bind, string? duplicateKeyCode) =>
        ExecuteCountCoreAsync(sql, cmd => bind(new CommandBinder(cmd)), duplicateKeyCode);

    Task<AxisResult<T>> IAxisDbRepository.GetAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map, string notFoundCode) =>
        GetCoreAsync(sql, cmd => bind(new CommandBinder(cmd)), map, notFoundCode);

    Task<AxisResult<IEnumerable<T>>> IAxisDbRepository.ListAsync<T>(string sql, Action<IDbParamBinder> bind, Func<DbDataReader, T> map) =>
        ListCoreAsync(sql, cmd => bind(new CommandBinder(cmd)), map);

    private sealed class CommandBinder(DbCommand command) : IDbParamBinder
    {
        public IDbParamBinder Add(string name, object? value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name.TrimStart('@');
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
            return this;
        }

        public IDbParamBinder AddJson(string name, string? json) => Add(name, json);
    }
}
