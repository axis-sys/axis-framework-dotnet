using Axis;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.SharedKernel;
using AxisSaga.MySql.Persistence;
using MySqlConnector;

namespace AxisSaga.MySql.Adapters;

/// <inheritdoc/>
internal class MySqlSagaStageLogStore(
    AxisSagaMySqlDataSource dataSource,
    IAxisLogger<MySqlSagaStageLogStore> logger
) : ISagaStageLogStore
{
    public async Task<bool> IsCompletedAsync(string sagaId, string stageName)
    {
        await using var conn = await dataSource.Inner.OpenConnectionAsync();
        await using var cmd = new MySqlCommand(
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
            await using var cmd = new MySqlCommand(
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
            logger.LogError(ex, $"{nameof(MySqlSagaStageLogStore)}.{nameof(WriteStartedAsync)} failed");
        }
        return logId;
    }

    public async Task MarkCompletedAsync(string logId)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new MySqlCommand(
                $"""
                 UPDATE {SagaStageLogsTable.Table}
                 SET {SagaStageLogsTable.Status} = @status, {SagaStageLogsTable.FinishedAt} = UTC_TIMESTAMP(6)
                 WHERE {SagaStageLogsTable.LogId} = @logId
                 """, conn);
            cmd.Parameters.AddWithValue("status", nameof(AxisSagaStageStatus.Completed));
            cmd.Parameters.AddWithValue("logId", logId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(MySqlSagaStageLogStore)}.{nameof(MarkCompletedAsync)} failed");
        }
    }

    public async Task MarkFailedAsync(string logId, string errorCode, string? errorMessage)
    {
        try
        {
            await using var conn = await dataSource.Inner.OpenConnectionAsync();
            await using var cmd = new MySqlCommand(
                $"""
                 UPDATE {SagaStageLogsTable.Table}
                 SET {SagaStageLogsTable.Status} = @status,
                     {SagaStageLogsTable.ErrorCode} = @errorCode,
                     {SagaStageLogsTable.ErrorMessage} = @errorMessage,
                     {SagaStageLogsTable.FinishedAt} = UTC_TIMESTAMP(6)
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
            logger.LogError(ex, $"{nameof(MySqlSagaStageLogStore)}.{nameof(MarkFailedAsync)} failed");
        }
    }
}
