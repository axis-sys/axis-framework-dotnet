using Axis;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Persistence;
using Npgsql;

namespace AxisSaga.Postgres.Adapters;

/// <inheritdoc/>
internal class SagaInstanceStore(
    AxisSagaPostgresDataSource dataSource,
    IAxisLogger<SagaInstanceStore> logger
) : ISagaInstanceStore
{
    // Every mutation must still own the lease: the run's token matches and the lease has not expired.
    private const string LeaseGuard =
        $" AND {SagaInstancesTable.ClaimedBy} = @runner AND {SagaInstancesTable.ClaimedUntil} > NOW()";

    private const string TerminalStatuses = "'Completed','Failed','Compensated'";

    public async Task<AxisResult> InsertAsync(string sagaId, string sagaName, string payloadJson, int? retainForSeconds)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            var retainCol = retainForSeconds.HasValue ? $", {SagaInstancesTable.RetainForSeconds}" : "";
            var retainVal = retainForSeconds.HasValue ? ", @retainFor" : "";
            await using var cmd = new NpgsqlCommand(
                $"""
                 INSERT INTO {SagaInstancesTable.Table}
                     ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                      {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version}{retainCol})
                 VALUES (@id, @name, @status, @payload::JSONB, 1{retainVal})
                 """, conn);
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("name", sagaName);
            cmd.Parameters.AddWithValue("status", AxisSagaStatus.Pending.ToString());
            cmd.Parameters.AddWithValue("payload", payloadJson);
            if (retainForSeconds.HasValue)
                cmd.Parameters.AddWithValue("retainFor", retainForSeconds.Value);

            await cmd.ExecuteNonQueryAsync();
            return AxisResult.Ok();
        }
        catch (NpgsqlException ex) when (ex.Message.Contains("duplicate key value violates unique constraint"))
        {
            return AxisError.Conflict("SAGA_ID_ALREADY_EXISTS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(InsertAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisSagaInstance?> AcquireLeaseAsync(string sagaId, string runner, int leaseSeconds)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            // The trailing predicate is the global concurrency gate. The cap lives in SAGA_SETTINGS
            // (MAX_CONCURRENT_SAGAS, NULL = unbounded), so the claim succeeds only while fewer than the
            // cap sagas hold a LIVE lease. It counts by lease liveness, NOT by STATUS. Soft cap: concurrent
            // claims under READ COMMITTED can transiently exceed it by a small, self-correcting amount.
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaInstancesTable.Table}
                 SET {SagaInstancesTable.ClaimedBy} = @runner,
                     {SagaInstancesTable.ClaimedUntil} = NOW() + make_interval(secs => @lease),
                     {SagaInstancesTable.UpdatedAt} = NOW()
                 WHERE {SagaInstancesTable.SagaId} = @id
                   AND {SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                   AND ({SagaInstancesTable.ClaimedUntil} IS NULL OR {SagaInstancesTable.ClaimedUntil} < NOW())
                   AND ((SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1) IS NULL
                        OR (SELECT COUNT(*) FROM {SagaInstancesTable.Table} s
                            WHERE s.{SagaInstancesTable.ClaimedUntil} > NOW()
                              AND s.{SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                              AND s.{SagaInstancesTable.SagaId} <> @id)
                           < (SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1))
                 RETURNING {SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                           {SagaInstancesTable.CurrentStage}, {SagaInstancesTable.PayloadJson},
                           {SagaInstancesTable.LastErrorCode}, {SagaInstancesTable.LastErrorMessage},
                           {SagaInstancesTable.Version}, {SagaInstancesTable.CreatedAt}, {SagaInstancesTable.UpdatedAt}
                 """, conn);
            cmd.Parameters.AddWithValue("runner", runner);
            cmd.Parameters.AddWithValue("lease", leaseSeconds);
            cmd.Parameters.AddWithValue("id", sagaId);

            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync() ? SagaInstanceMapper.Map(reader) : null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(AcquireLeaseAsync)} failed");
            return null;
        }
    }

    public async Task<bool> ExtendLeaseAsync(string sagaId, string runner, int leaseSeconds)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaInstancesTable.Table}
                 SET {SagaInstancesTable.ClaimedUntil} = NOW() + make_interval(secs => @lease)
                 WHERE {SagaInstancesTable.SagaId} = @id
                   AND {SagaInstancesTable.ClaimedBy} = @runner
                   AND {SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                 """, conn);
            cmd.Parameters.AddWithValue("lease", leaseSeconds);
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("runner", runner);

            return await cmd.ExecuteNonQueryAsync() == 1;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(ExtendLeaseAsync)} failed");
            return false;
        }
    }

    public async Task<AxisResult<AxisSagaInstance>> LoadAsync(string sagaId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 SELECT {SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                        {SagaInstancesTable.CurrentStage}, {SagaInstancesTable.PayloadJson},
                        {SagaInstancesTable.LastErrorCode}, {SagaInstancesTable.LastErrorMessage},
                        {SagaInstancesTable.Version}, {SagaInstancesTable.CreatedAt}, {SagaInstancesTable.UpdatedAt}
                 FROM {SagaInstancesTable.Table}
                 WHERE {SagaInstancesTable.SagaId} = @id
                 """, conn);
            cmd.Parameters.AddWithValue("id", sagaId);

            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? AxisResult.Ok(SagaInstanceMapper.Map(reader))
                : AxisError.NotFound(AxisSagaErrors.SagaInstanceNotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(LoadAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<string?> ReloadPayloadJsonAsync(string sagaId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"SELECT {SagaInstancesTable.PayloadJson} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id",
                conn);
            cmd.Parameters.AddWithValue("id", sagaId);

            return (string?)await cmd.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(ReloadPayloadJsonAsync)} failed");
            return null;
        }
    }

    public async Task<AxisResult> MoveToStatusAsync(
        string sagaId, int expectedVersion, string runner, AxisSagaStatus newStatus, string? currentStage,
        string? errorCode = null, string? errorMessage = null)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaInstancesTable.Table}
                 SET {SagaInstancesTable.Status} = @status,
                     {SagaInstancesTable.CurrentStage} = @stage,
                     {SagaInstancesTable.LastErrorCode} = COALESCE(@errCode, {SagaInstancesTable.LastErrorCode}),
                     {SagaInstancesTable.LastErrorMessage} = COALESCE(@errMsg, {SagaInstancesTable.LastErrorMessage}),
                     {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                     {SagaInstancesTable.UpdatedAt} = NOW()
                 WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                 """, conn);
            cmd.Parameters.AddWithValue("status", newStatus.ToString());
            cmd.Parameters.AddWithValue("stage", (object?)currentStage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("errCode", (object?)errorCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("errMsg", (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("version", expectedVersion);
            cmd.Parameters.AddWithValue("runner", runner);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 1 ? AxisResult.Ok() : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(MoveToStatusAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> PersistStageSuccessAsync(
        string sagaId, int expectedVersion, string runner, string stageName, string payloadJson,
        bool keepCurrentStage = false)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            var sql = keepCurrentStage
                ? $"""
                   UPDATE {SagaInstancesTable.Table}
                   SET {SagaInstancesTable.PayloadJson} = @payload::JSONB,
                       {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                       {SagaInstancesTable.UpdatedAt} = NOW()
                   WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                   """
                : $"""
                   UPDATE {SagaInstancesTable.Table}
                   SET {SagaInstancesTable.PayloadJson} = @payload::JSONB,
                       {SagaInstancesTable.CurrentStage} = @stage,
                       {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                       {SagaInstancesTable.UpdatedAt} = NOW()
                   WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                   """;

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("payload", payloadJson);
            if (!keepCurrentStage)
                cmd.Parameters.AddWithValue("stage", stageName);
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("version", expectedVersion);
            cmd.Parameters.AddWithValue("runner", runner);

            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 1 ? AxisResult.Ok() : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(PersistStageSuccessAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public Task<AxisResult> CompleteAsync(string sagaId, int expectedVersion, string runner)
        => SetTerminalStatusAsync(sagaId, expectedVersion, runner, AxisSagaStatus.Completed);

    public Task<AxisResult> CompensateAsync(string sagaId, int expectedVersion, string runner)
        => SetTerminalStatusAsync(sagaId, expectedVersion, runner, AxisSagaStatus.Compensated);

    public async Task<AxisResult> FailAsync(string sagaId, int expectedVersion, string runner, string errorCode, string? errorMessage = null)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaInstancesTable.Table}
                 SET {SagaInstancesTable.Status} = @status,
                     {SagaInstancesTable.LastErrorCode} = @errCode,
                     {SagaInstancesTable.LastErrorMessage} = @errMsg,
                     {SagaInstancesTable.ClaimedBy} = NULL,
                     {SagaInstancesTable.ClaimedUntil} = NULL,
                     {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                     {SagaInstancesTable.UpdatedAt} = NOW()
                 WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                 """, conn);
            cmd.Parameters.AddWithValue("status", nameof(AxisSagaStatus.Failed));
            cmd.Parameters.AddWithValue("errCode", errorCode);
            cmd.Parameters.AddWithValue("errMsg", (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("version", expectedVersion);
            cmd.Parameters.AddWithValue("runner", runner);
            // A lost-lease run must not report it failed the saga: only the row owner (rows==1) did.
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 1 ? AxisError.BusinessRule(errorCode) : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(FailAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<IReadOnlyList<string>> ClaimStaleSagaIdsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"""
             SELECT {SagaInstancesTable.SagaId}
             FROM {SagaInstancesTable.Table}
             WHERE {SagaInstancesTable.Status} IN (@pending, @running, @compensating)
               AND ({SagaInstancesTable.ClaimedUntil} IS NULL OR {SagaInstancesTable.ClaimedUntil} < NOW())
             ORDER BY {SagaInstancesTable.ClaimedUntil} NULLS FIRST
             LIMIT @batch
             FOR UPDATE SKIP LOCKED
             """, conn);
        cmd.Parameters.AddWithValue("pending", nameof(AxisSagaStatus.Pending));
        cmd.Parameters.AddWithValue("running", nameof(AxisSagaStatus.Running));
        cmd.Parameters.AddWithValue("compensating", nameof(AxisSagaStatus.Compensating));
        cmd.Parameters.AddWithValue("batch", limit);

        var ids = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            ids.Add(reader.GetString(0));
        return ids;
    }

    public async Task<int> CountLiveLeasesAsync(CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"""
             SELECT COUNT(*) FROM {SagaInstancesTable.Table}
             WHERE {SagaInstancesTable.ClaimedUntil} > NOW()
               AND {SagaInstancesTable.Status} IN (@pending, @running, @compensating)
             """, conn);
        cmd.Parameters.AddWithValue("pending", nameof(AxisSagaStatus.Pending));
        cmd.Parameters.AddWithValue("running", nameof(AxisSagaStatus.Running));
        cmd.Parameters.AddWithValue("compensating", nameof(AxisSagaStatus.Compensating));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int?> GetMaxConcurrentSagasAsync(CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(
            $"SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    public async Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
            await using var cmd = new NpgsqlCommand(
                $"""
                 DELETE FROM {SagaInstancesTable.Table}
                 WHERE {SagaInstancesTable.SagaId} IN (
                     SELECT {SagaInstancesTable.SagaId}
                     FROM {SagaInstancesTable.Table}
                     WHERE {SagaInstancesTable.DeleteNotBefore} IS NOT NULL
                       AND {SagaInstancesTable.DeleteNotBefore} <= NOW()
                     LIMIT @batch
                 )
                 """, conn);
            cmd.Parameters.AddWithValue("batch", batchSize);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(DeleteExpiredAsync)} failed");
            return 0;
        }
    }

    private async Task<AxisResult> SetTerminalStatusAsync(string sagaId, int expectedVersion, string runner, AxisSagaStatus status)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaInstancesTable.Table}
                 SET {SagaInstancesTable.Status} = @status,
                     {SagaInstancesTable.DeleteNotBefore} = CASE
                         WHEN {SagaInstancesTable.RetainForSeconds} IS NOT NULL
                         THEN NOW() + make_interval(secs => {SagaInstancesTable.RetainForSeconds})
                         ELSE NULL
                     END,
                     {SagaInstancesTable.ClaimedBy} = NULL,
                     {SagaInstancesTable.ClaimedUntil} = NULL,
                     {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                     {SagaInstancesTable.UpdatedAt} = NOW()
                 WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                 """, conn);
            cmd.Parameters.AddWithValue("status", status.ToString());
            cmd.Parameters.AddWithValue("id", sagaId);
            cmd.Parameters.AddWithValue("version", expectedVersion);
            cmd.Parameters.AddWithValue("runner", runner);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows == 1 ? AxisResult.Ok() : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaInstanceStore)}.{nameof(SetTerminalStatusAsync)} failed ({status})");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }
}
