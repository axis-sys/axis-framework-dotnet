using System.Data.Common;
using Axis;
using AxisCache.Repository.Persistence;
using AxisCache.Repository.Ports;
using AxisMediator.Contracts;

namespace AxisCache.Repository.Store;

/// <summary>
/// The dialect-agnostic L2 store. All SQL except the upsert is standard and shared here; the upsert comes
/// from <see cref="IAxisCacheSqlDialect"/> and the connection from <see cref="IAxisCacheConnectionFactory"/>,
/// so Postgres and MySQL differ only by those two seams. Drives plain ADO.NET over <see cref="DbConnection"/>
/// and never throws — every path returns an <see cref="AxisResult"/>. The ambient cancellation token is read
/// from <see cref="IAxisMediatorAccessor"/> (never a parameter), mirroring the memory adapter.
/// </summary>
internal sealed class CacheEntryStore(
    IAxisCacheConnectionFactory connections,
    IAxisCacheSqlDialect dialect,
    IAxisMediatorAccessor mediatorAccessor,
    IAxisLogger<CacheEntryStore> logger
) : ICacheEntryStore
{
    private const string SelectSql =
        $"SELECT {CacheEntriesTable.ValueJson}, {CacheEntriesTable.ExpiresAt} " +
        $"FROM {CacheEntriesTable.Table} WHERE {CacheEntriesTable.CacheKey} = @key";

    private const string DeleteSql =
        $"DELETE FROM {CacheEntriesTable.Table} WHERE {CacheEntriesTable.CacheKey} = @key";

    private const string DeleteExpiredSql =
        $"DELETE FROM {CacheEntriesTable.Table} " +
        $"WHERE {CacheEntriesTable.ExpiresAt} IS NOT NULL AND {CacheEntriesTable.ExpiresAt} <= @now";

    private CancellationToken Token => mediatorAccessor.AxisMediator?.CancellationToken ?? CancellationToken.None;

    public async Task<AxisResult<CacheEntry?>> GetAsync(string key, DateTimeOffset nowUtc)
    {
        try
        {
            var token = Token;
            await using var conn = await connections.OpenConnectionAsync(token);

            string? valueJson;
            DateTimeOffset? expiresAt;
            await using (var cmd = Command(conn, SelectSql, ("key", key)))
            await using (var reader = await cmd.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token))
                    return AxisResult.Ok<CacheEntry?>(null);

                valueJson = reader.GetString(0);
                expiresAt = await reader.IsDBNullAsync(1, token)
                    ? null
                    : new DateTimeOffset(DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc));
            }

            if (expiresAt is { } expiry && expiry <= nowUtc)
            {
                // Expired: drop the row in passing and report a miss.
                await using var delete = Command(conn, DeleteSql, ("key", key));
                await delete.ExecuteNonQueryAsync(token);
                return AxisResult.Ok<CacheEntry?>(null);
            }

            return AxisResult.Ok<CacheEntry?>(new CacheEntry(valueJson, expiresAt));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(CacheEntryStore)}.{nameof(GetAsync)} failed");
            return AxisError.InternalServerError(AxisCacheErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> UpsertAsync(string key, string valueJson, DateTimeOffset? expiresAt, DateTimeOffset nowUtc)
    {
        try
        {
            var token = Token;
            await using var conn = await connections.OpenConnectionAsync(token);
            await using var cmd = Command(conn, dialect.UpsertSql,
                ("key", key),
                ("value", valueJson),
                ("expiresAt", (object?)expiresAt?.UtcDateTime ?? DBNull.Value),
                ("updatedAt", nowUtc.UtcDateTime));

            await cmd.ExecuteNonQueryAsync(token);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(CacheEntryStore)}.{nameof(UpsertAsync)} failed");
            return AxisError.InternalServerError(AxisCacheErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult> RemoveAsync(string key)
    {
        try
        {
            var token = Token;
            await using var conn = await connections.OpenConnectionAsync(token);
            await using var cmd = Command(conn, DeleteSql, ("key", key));
            await cmd.ExecuteNonQueryAsync(token);
            return AxisResult.Ok();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(CacheEntryStore)}.{nameof(RemoveAsync)} failed");
            return AxisError.InternalServerError(AxisCacheErrors.PersistenceFailed);
        }
    }

    public async Task<AxisResult<int>> DeleteExpiredAsync(DateTimeOffset nowUtc)
    {
        try
        {
            var token = Token;
            await using var conn = await connections.OpenConnectionAsync(token);
            await using var cmd = Command(conn, DeleteExpiredSql, ("now", nowUtc.UtcDateTime));
            return AxisResult.Ok(await cmd.ExecuteNonQueryAsync(token));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"{nameof(CacheEntryStore)}.{nameof(DeleteExpiredAsync)} failed");
            return AxisError.InternalServerError(AxisCacheErrors.PersistenceFailed);
        }
    }

    private static DbCommand Command(DbConnection conn, string sql, params (string Name, object Value)[] parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
