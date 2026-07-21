using Axis.Ddl;
using Npgsql;

namespace AxisRepository.Postgres;

/// <summary>
/// An idempotent Postgres migration runner shared by Bounded Contexts — the Postgres <see cref="IAxisMigrationRunner"/>.
/// It bootstraps the schema and the <c>MIGRATIONS</c> control table, and applies the
/// pending versions in a single transaction, serializing concurrent migrations for the
/// same schema via a transactional advisory lock.
/// </summary>
public sealed class PostgresMigrationRunner : IAxisMigrationRunner
{
    public async Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations)
    {
        var migrationsTable = $"{schema}.MIGRATIONS";

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        // Idempotent bootstrap: schema + control table, outside the transaction.
        await using (var cmd = new NpgsqlCommand(
                         $"CREATE SCHEMA IF NOT EXISTS {schema};" +
                         $"CREATE TABLE IF NOT EXISTS {migrationsTable} " +
                         "(VERSION VARCHAR(50) PRIMARY KEY, APPLIED_AT TIMESTAMPTZ NOT NULL DEFAULT NOW())", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Applies each pending migration within a single transaction.
        await using var transaction = await conn.BeginTransactionAsync();

        // Serializes concurrent migrations for the same schema (multiple instances
        // running simultaneously). Transactional lock: automatically released on commit/rollback.
        await using (var lockCmd = new NpgsqlCommand("SELECT pg_advisory_xact_lock(hashtext(@schema))", conn, transaction))
        {
            lockCmd.Parameters.AddWithValue("schema", schema);
            await lockCmd.ExecuteNonQueryAsync();
        }

        foreach (var (version, script) in migrations)
        {
            await using (var checkCmd = new NpgsqlCommand($"SELECT 1 FROM {migrationsTable} WHERE VERSION = @version", conn, transaction))
            {
                checkCmd.Parameters.AddWithValue("version", version);
                if (await checkCmd.ExecuteScalarAsync() is not null)
                    continue;
            }

            await using (var migCmd = new NpgsqlCommand(script, conn, transaction))
                await migCmd.ExecuteNonQueryAsync();

            await using var recordCmd = new NpgsqlCommand($"INSERT INTO {migrationsTable} (VERSION) VALUES (@version)", conn, transaction);
            recordCmd.Parameters.AddWithValue("version", version);
            await recordCmd.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }
}
