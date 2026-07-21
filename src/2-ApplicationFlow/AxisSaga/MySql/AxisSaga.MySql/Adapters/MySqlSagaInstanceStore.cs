using Axis;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.MySql.Persistence;
using MySqlConnector;

namespace AxisSaga.MySql.Adapters;

/// <inheritdoc/>
internal class MySqlSagaInstanceStore(
    AxisSagaMySqlDataSource dataSource,
    IAxisLogger<MySqlSagaInstanceStore> logger
) : ISagaInstanceStore
{
    // Lease ownership guard appended to every mutation: the run's token matches and the lease is live.
    private const string LeaseGuard =
        $" AND {SagaInstancesTable.ClaimedBy} = @runner AND {SagaInstancesTable.ClaimedUntil} > UTC_TIMESTAMP(6)";

    private const string TerminalStatuses = "'Completed','Failed','Compensated'";
    private const string ActiveStatuses = "'Pending','Running','Compensating'";

    private const string SelectColumns =
        $"{SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status}, " +
        $"{SagaInstancesTable.CurrentStage}, {SagaInstancesTable.PayloadJson}, " +
        $"{SagaInstancesTable.LastErrorCode}, {SagaInstancesTable.LastErrorMessage}, " +
        $"{SagaInstancesTable.Version}, {SagaInstancesTable.CreatedAt}, {SagaInstancesTable.UpdatedAt}";

    public async Task<AxisResult> InsertAsync(string sagaId, string sagaName, string payloadJson, int? retainForSeconds)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                var retainCol = retainForSeconds.HasValue ? $", {SagaInstancesTable.RetainForSeconds}" : "";
                var retainVal = retainForSeconds.HasValue ? ", @retainFor" : "";
                await using var cmd = new MySqlCommand(
                    $"""
                     INSERT INTO {SagaInstancesTable.Table}
                         ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                          {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version}{retainCol})
                     VALUES (@id, @name, @status, @payload, 1{retainVal})
                     """, conn);
                cmd.Parameters.AddWithValue("id", sagaId);
                cmd.Parameters.AddWithValue("name", sagaName);
                cmd.Parameters.AddWithValue("status", AxisSagaStatus.Pending.ToString());
                cmd.Parameters.AddWithValue("payload", payloadJson);
                if (retainForSeconds.HasValue)
                    cmd.Parameters.AddWithValue("retainFor", retainForSeconds.Value);

                await cmd.ExecuteNonQueryAsync();
                return AxisResult.Ok();
            });
        }
        catch (MySqlException ex) when (ex.Number == 1062)
        {
            return AxisError.Conflict("SAGA_ID_ALREADY_EXISTS");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(InsertAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisSagaInstance?> AcquireLeaseAsync(string sagaId, string runner, int leaseSeconds)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();

                // Global concurrency gate, evaluated as a NON-LOCKING read so it never takes locks on the
                // live-lease rows. Folding the live-lease COUNT into the claim UPDATE (as a locking
                // sub-select) was the deadlock vector: that scan collides with the row-level write locks of
                // concurrent claims and with concurrent INSERTs. The cap is a SOFT cap by design — concurrent
                // claims may transiently exceed it by a small, self-correcting amount — so reading it a beat
                // before the claim is acceptable.
                await using (var gate = new MySqlCommand(
                    $"""
                     SELECT
                         (SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1),
                         (SELECT COUNT(*) FROM {SagaInstancesTable.Table}
                          WHERE {SagaInstancesTable.ClaimedUntil} > UTC_TIMESTAMP(6)
                            AND {SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                            AND {SagaInstancesTable.SagaId} <> @id)
                     """, conn))
                {
                    gate.Parameters.AddWithValue("id", sagaId);
                    await using var gateReader = await gate.ExecuteReaderAsync();
                    if (await gateReader.ReadAsync()
                        && !await gateReader.IsDBNullAsync(0)
                        && gateReader.GetInt32(1) >= gateReader.GetInt32(0))
                        return null; // cap reached → deny, exactly like a lease held by another run
                }

                // Claim by primary key only — this locks exactly one row (the unique PK match) with no range
                // scan, so it cannot gap-lock or cross-wait with other claims/inserts.
                await using var update = new MySqlCommand(
                    $"""
                     UPDATE {SagaInstancesTable.Table}
                     SET {SagaInstancesTable.ClaimedBy} = @runner,
                         {SagaInstancesTable.ClaimedUntil} = UTC_TIMESTAMP(6) + INTERVAL @lease SECOND,
                         {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
                     WHERE {SagaInstancesTable.SagaId} = @id
                       AND {SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                       AND ({SagaInstancesTable.ClaimedUntil} IS NULL OR {SagaInstancesTable.ClaimedUntil} < UTC_TIMESTAMP(6))
                     """, conn);
                update.Parameters.AddWithValue("runner", runner);
                update.Parameters.AddWithValue("lease", leaseSeconds);
                update.Parameters.AddWithValue("id", sagaId);

                if (await update.ExecuteNonQueryAsync() != 1)
                    return null;

                // No RETURNING in MySQL: read back the row we just claimed. The runner token is unique to this
                // run, so this reads exactly the claimed row even without a surrounding transaction.
                await using var select = new MySqlCommand(
                    $"SELECT {SelectColumns} FROM {SagaInstancesTable.Table} " +
                    $"WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.ClaimedBy} = @runner", conn);
                select.Parameters.AddWithValue("id", sagaId);
                select.Parameters.AddWithValue("runner", runner);

                await using var reader = await select.ExecuteReaderAsync();
                return await reader.ReadAsync() ? SagaInstanceMapper.Map(reader) : null;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(AcquireLeaseAsync)} failed");
            return null;
        }
    }

    public async Task<bool> ExtendLeaseAsync(string sagaId, string runner, int leaseSeconds)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                await using var cmd = new MySqlCommand(
                    $"""
                     UPDATE {SagaInstancesTable.Table}
                     SET {SagaInstancesTable.ClaimedUntil} = UTC_TIMESTAMP(6) + INTERVAL @lease SECOND
                     WHERE {SagaInstancesTable.SagaId} = @id
                       AND {SagaInstancesTable.ClaimedBy} = @runner
                       AND {SagaInstancesTable.Status} NOT IN ({TerminalStatuses})
                     """, conn);
                cmd.Parameters.AddWithValue("lease", leaseSeconds);
                cmd.Parameters.AddWithValue("id", sagaId);
                cmd.Parameters.AddWithValue("runner", runner);

                return await cmd.ExecuteNonQueryAsync() == 1;
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(ExtendLeaseAsync)} failed");
            return false;
        }
    }

    public async Task<AxisResult<AxisSagaInstance>> LoadAsync(string sagaId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new MySqlCommand(
                $"SELECT {SelectColumns} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id", conn);
            cmd.Parameters.AddWithValue("id", sagaId);

            await using var reader = await cmd.ExecuteReaderAsync();
            return await reader.ReadAsync()
                ? AxisResult.Ok(SagaInstanceMapper.Map(reader))
                : AxisError.NotFound(AxisSagaErrors.SagaInstanceNotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(LoadAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<string?> ReloadPayloadJsonAsync(string sagaId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new MySqlCommand(
                $"SELECT {SagaInstancesTable.PayloadJson} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id",
                conn);
            cmd.Parameters.AddWithValue("id", sagaId);

            return (await cmd.ExecuteScalarAsync()) as string;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(ReloadPayloadJsonAsync)} failed");
            return null;
        }
    }

    public async Task<AxisResult> MoveToStatusAsync(
        string sagaId, int expectedVersion, string runner, AxisSagaStatus newStatus, string? currentStage,
        string? errorCode = null, string? errorMessage = null)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                await using var cmd = new MySqlCommand(
                    $"""
                     UPDATE {SagaInstancesTable.Table}
                     SET {SagaInstancesTable.Status} = @status,
                         {SagaInstancesTable.CurrentStage} = @stage,
                         {SagaInstancesTable.LastErrorCode} = COALESCE(@errCode, {SagaInstancesTable.LastErrorCode}),
                         {SagaInstancesTable.LastErrorMessage} = COALESCE(@errMsg, {SagaInstancesTable.LastErrorMessage}),
                         {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                         {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
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
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(MoveToStatusAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> PersistStageSuccessAsync(
        string sagaId, int expectedVersion, string runner, string stageName, string payloadJson,
        bool keepCurrentStage = false)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                var sql = keepCurrentStage
                    ? $"""
                       UPDATE {SagaInstancesTable.Table}
                       SET {SagaInstancesTable.PayloadJson} = @payload,
                           {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                           {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
                       WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                       """
                    : $"""
                       UPDATE {SagaInstancesTable.Table}
                       SET {SagaInstancesTable.PayloadJson} = @payload,
                           {SagaInstancesTable.CurrentStage} = @stage,
                           {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                           {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
                       WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                       """;

                await using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("payload", payloadJson);
                if (!keepCurrentStage)
                    cmd.Parameters.AddWithValue("stage", stageName);
                cmd.Parameters.AddWithValue("id", sagaId);
                cmd.Parameters.AddWithValue("version", expectedVersion);
                cmd.Parameters.AddWithValue("runner", runner);

                var rows = await cmd.ExecuteNonQueryAsync();
                return rows == 1 ? AxisResult.Ok() : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(PersistStageSuccessAsync)} failed");
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
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                await using var cmd = new MySqlCommand(
                    $"""
                     UPDATE {SagaInstancesTable.Table}
                     SET {SagaInstancesTable.Status} = @status,
                         {SagaInstancesTable.LastErrorCode} = @errCode,
                         {SagaInstancesTable.LastErrorMessage} = @errMsg,
                         {SagaInstancesTable.ClaimedBy} = NULL,
                         {SagaInstancesTable.ClaimedUntil} = NULL,
                         {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                         {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
                     WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                     """, conn);
                cmd.Parameters.AddWithValue("status", nameof(AxisSagaStatus.Failed));
                cmd.Parameters.AddWithValue("errCode", errorCode);
                cmd.Parameters.AddWithValue("errMsg", (object?)errorMessage ?? DBNull.Value);
                cmd.Parameters.AddWithValue("id", sagaId);
                cmd.Parameters.AddWithValue("version", expectedVersion);
                cmd.Parameters.AddWithValue("runner", runner);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows == 1 ? AxisError.BusinessRule(errorCode) : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(FailAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<IReadOnlyList<string>> ClaimStaleSagaIdsAsync(int limit, CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        // NULL CLAIMED_UNTIL sorts first under MySQL's default ASC ordering, matching Postgres NULLS FIRST.
        // The Postgres FOR UPDATE SKIP LOCKED is omitted: the real dedup is the engine's atomic lease
        // acquire, so a re-fire that races another resumer is a harmless no-op.
        await using var cmd = new MySqlCommand(
            $"""
             SELECT {SagaInstancesTable.SagaId}
             FROM {SagaInstancesTable.Table}
             WHERE {SagaInstancesTable.Status} IN ({ActiveStatuses})
               AND ({SagaInstancesTable.ClaimedUntil} IS NULL OR {SagaInstancesTable.ClaimedUntil} < UTC_TIMESTAMP(6))
             ORDER BY {SagaInstancesTable.ClaimedUntil} ASC
             LIMIT @batch
             """, conn);
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
        await using var cmd = new MySqlCommand(
            $"""
             SELECT COUNT(*) FROM {SagaInstancesTable.Table}
             WHERE {SagaInstancesTable.ClaimedUntil} > UTC_TIMESTAMP(6)
               AND {SagaInstancesTable.Status} IN ({ActiveStatuses})
             """, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<int?> GetMaxConcurrentSagasAsync(CancellationToken cancellationToken)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
        await using var cmd = new MySqlCommand(
            $"SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    public async Task<int> DeleteExpiredAsync(int batchSize, CancellationToken cancellationToken)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync(cancellationToken);
            // The sub-select reads the same table being deleted (MySQL 1093) → wrap it in a derived table.
            await using var cmd = new MySqlCommand(
                $"""
                 DELETE FROM {SagaInstancesTable.Table}
                 WHERE {SagaInstancesTable.SagaId} IN (
                     SELECT {SagaInstancesTable.SagaId} FROM (
                         SELECT {SagaInstancesTable.SagaId}
                         FROM {SagaInstancesTable.Table}
                         WHERE {SagaInstancesTable.DeleteNotBefore} IS NOT NULL
                           AND {SagaInstancesTable.DeleteNotBefore} <= UTC_TIMESTAMP(6)
                         LIMIT @batch
                     ) AS expired
                 )
                 """, conn);
            cmd.Parameters.AddWithValue("batch", batchSize);
            return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(DeleteExpiredAsync)} failed");
            return 0;
        }
    }

    private async Task<AxisResult> SetTerminalStatusAsync(string sagaId, int expectedVersion, string runner, AxisSagaStatus status)
    {
        try
        {
            return await MySqlTransientRetry.ExecuteAsync(async () =>
            {
                await using var conn = await dataSource.Inner.OpenConnectionAsync();
                await using var cmd = new MySqlCommand(
                    $"""
                     UPDATE {SagaInstancesTable.Table}
                     SET {SagaInstancesTable.Status} = @status,
                         {SagaInstancesTable.DeleteNotBefore} = CASE
                             WHEN {SagaInstancesTable.RetainForSeconds} IS NOT NULL
                             THEN UTC_TIMESTAMP(6) + INTERVAL {SagaInstancesTable.RetainForSeconds} SECOND
                             ELSE NULL
                         END,
                         {SagaInstancesTable.ClaimedBy} = NULL,
                         {SagaInstancesTable.ClaimedUntil} = NULL,
                         {SagaInstancesTable.Version} = {SagaInstancesTable.Version} + 1,
                         {SagaInstancesTable.UpdatedAt} = UTC_TIMESTAMP(6)
                     WHERE {SagaInstancesTable.SagaId} = @id AND {SagaInstancesTable.Version} = @version{LeaseGuard}
                     """, conn);
                cmd.Parameters.AddWithValue("status", status.ToString());
                cmd.Parameters.AddWithValue("id", sagaId);
                cmd.Parameters.AddWithValue("version", expectedVersion);
                cmd.Parameters.AddWithValue("runner", runner);
                var rows = await cmd.ExecuteNonQueryAsync();
                return rows == 1 ? AxisResult.Ok() : AxisError.Conflict(AxisSagaErrors.ConcurrencyConflict);
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaInstanceStore)}.{nameof(SetTerminalStatusAsync)} failed ({status})");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }
}
