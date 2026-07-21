using System.Data.Common;
using Axis.Persistence;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.SharedKernel;

namespace Axis.Saga;

/// <summary>
/// Dialect-agnostic implementation of <see cref="IAxisSagaSettingsStore"/>. It talks to
/// <c>AXIS_SAGA.SAGA_SETTINGS</c> through plain <see cref="System.Data.Common"/> types over an injected
/// <see cref="IAxisSagaConnectionSource"/>, and the SQL is portable across every supported dialect, so no
/// per-dialect code exists: the AXIS_SAGA identifiers are uppercase and unquoted (Postgres folds them to
/// lower case; MySQL resolves the dedicated AXIS_SAGA database), <c>LIMIT 1</c> and the <c>@name</c>
/// parameter placeholder work on both Npgsql and MySqlConnector, and SAGA_SETTINGS is pinned to a single
/// row (the <c>ONLY_ROW</c> boolean PK + CHECK). Every method converts exceptions into
/// <see cref="AxisResult"/> and never throws.
/// </summary>
internal sealed class AxisSagaSettingsStore(
    IAxisSagaConnectionSource connectionSource,
    IAxisLogger<AxisSagaSettingsStore> logger
) : IAxisSagaSettingsStore
{
    public async Task<AxisResult<int?>> GetMaxConcurrentSagasAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await connectionSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return AxisResult.Ok<int?>(result is null or DBNull ? null : Convert.ToInt32(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(AxisSagaSettingsStore)}.{nameof(GetMaxConcurrentSagasAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> SetMaxConcurrentSagasAsync(int? maxConcurrentSagas, CancellationToken cancellationToken = default)
    {
        if (maxConcurrentSagas is <= 0)
            return AxisError.ValidationRule(AxisSagaErrors.InvalidConcurrencyCap);

        try
        {
            await using var conn = await connectionSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            // Single-row table (ONLY_ROW PK + CHECK), so an unqualified UPDATE targets exactly that one row.
            cmd.CommandText = $"UPDATE {SagaSettingsTable.Table} SET {SagaSettingsTable.MaxConcurrentSagas} = @cap";
            AddParameter(cmd, "cap", (object?)maxConcurrentSagas ?? DBNull.Value);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            // Both drivers report MATCHED rows (Npgsql always; MySqlConnector under its default
            // UseAffectedRows=false), so a present row yields 1 even when the value is unchanged; 0 means the
            // settings row is missing (schema not migrated yet).
            return affected >= 1 ? AxisResult.Ok() : AxisError.NotFound(AxisSagaErrors.SagaSettingsNotFound);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(AxisSagaSettingsStore)}.{nameof(SetMaxConcurrentSagasAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult<bool>> TrySetMaxConcurrentSagasAsync(int expectedCurrent, int? newValue, CancellationToken cancellationToken = default)
    {
        if (newValue is <= 0)
            return AxisError.ValidationRule(AxisSagaErrors.InvalidConcurrencyCap);

        try
        {
            await using var conn = await connectionSource.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();
            // Atomic guard: the WHERE makes the write conditional on the stored value still being the
            // expected one, so two racing callers cannot both raise it and a manually tuned value is never
            // overwritten. A stored NULL (unbounded) never equals a concrete expectedCurrent, so it is left as-is.
            cmd.CommandText =
                $"UPDATE {SagaSettingsTable.Table} SET {SagaSettingsTable.MaxConcurrentSagas} = @newCap " +
                $"WHERE {SagaSettingsTable.MaxConcurrentSagas} = @expectedCap";
            AddParameter(cmd, "newCap", (object?)newValue ?? DBNull.Value);
            AddParameter(cmd, "expectedCap", expectedCurrent);

            var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
            return AxisResult.Ok(affected >= 1);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(AxisSagaSettingsStore)}.{nameof(TrySetMaxConcurrentSagasAsync)} failed");
            return AxisError.InternalServerError(AxisSagaErrors.PersistenceFailed);
        }
    }

    private static void AddParameter(DbCommand cmd, string name, object value)
    {
        var parameter = cmd.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        cmd.Parameters.Add(parameter);
    }
}
