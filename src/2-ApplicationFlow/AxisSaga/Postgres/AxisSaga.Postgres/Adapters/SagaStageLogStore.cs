using Axis;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.SharedKernel;
using AxisSaga.Postgres.Persistence;
using Npgsql;

namespace AxisSaga.Postgres.Adapters;

/// <inheritdoc/>
internal class SagaStageLogStore(
    AxisSagaPostgresDataSource dataSource,
    IAxisLogger<SagaStageLogStore> logger
) : ISagaStageLogStore
{
    public async Task<bool> IsCompletedAsync(string sagaId, string stageName)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"""
             SELECT 1 FROM {SagaStageLogsTable.Table}
             WHERE {SagaStageLogsTable.SagaId} = @id
               AND {SagaStageLogsTable.StageName} = @stage
               AND {SagaStageLogsTable.Status} = @status
             LIMIT 1
             """, conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        cmd.Parameters.AddWithValue("stage", stageName);
        cmd.Parameters.AddWithValue("status", nameof(AxisSagaStageStatus.Completed));

        return await cmd.ExecuteScalarAsync() is not null;
    }

    public async Task<string> WriteStartedAsync(string sagaId, string stageName)
    {
        var logId = Guid.CreateVersion7().ToString();
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 INSERT INTO {SagaStageLogsTable.Table}
                     ({SagaStageLogsTable.LogId}, {SagaStageLogsTable.SagaId}, {SagaStageLogsTable.StageName},
                      {SagaStageLogsTable.Status})
                 VALUES (@logId, @sagaId, @stage, @status)
                 """, conn);
            cmd.Parameters.AddWithValue("logId", logId);
            cmd.Parameters.AddWithValue("sagaId", sagaId);
            cmd.Parameters.AddWithValue("stage", stageName);
            cmd.Parameters.AddWithValue("status", nameof(AxisSagaStageStatus.Started));
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaStageLogStore)}.{nameof(WriteStartedAsync)} failed");
        }
        return logId;
    }

    public async Task MarkCompletedAsync(string logId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaStageLogsTable.Table}
                 SET {SagaStageLogsTable.Status} = @status, {SagaStageLogsTable.FinishedAt} = NOW()
                 WHERE {SagaStageLogsTable.LogId} = @logId
                 """, conn);
            cmd.Parameters.AddWithValue("status", nameof(AxisSagaStageStatus.Completed));
            cmd.Parameters.AddWithValue("logId", logId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaStageLogStore)}.{nameof(MarkCompletedAsync)} failed");
        }
    }

    public async Task MarkFailedAsync(string logId, string errorCode, string? errorMessage)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                $"""
                 UPDATE {SagaStageLogsTable.Table}
                 SET {SagaStageLogsTable.Status} = @status,
                     {SagaStageLogsTable.ErrorCode} = @errorCode,
                     {SagaStageLogsTable.ErrorMessage} = @errorMessage,
                     {SagaStageLogsTable.FinishedAt} = NOW()
                 WHERE {SagaStageLogsTable.LogId} = @logId
                 """, conn);
            cmd.Parameters.AddWithValue("status", nameof(AxisSagaStageStatus.Failed));
            cmd.Parameters.AddWithValue("errorCode", errorCode);
            cmd.Parameters.AddWithValue("errorMessage", (object?)errorMessage ?? DBNull.Value);
            cmd.Parameters.AddWithValue("logId", logId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(SagaStageLogStore)}.{nameof(MarkFailedAsync)} failed");
        }
    }
}
