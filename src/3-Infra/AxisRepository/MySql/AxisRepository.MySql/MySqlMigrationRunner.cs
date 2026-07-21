using Axis.Ddl;
using MySqlConnector;

namespace AxisRepository.MySql;

/// <summary>
/// An idempotent MySQL migration runner — the MySQL <see cref="IAxisMigrationRunner"/>, dialect twin of
/// <c>AxisRepository.Postgres.PostgresMigrationRunner</c>.
/// It connects at the server level (no default database) so it can bootstrap on a bare server — MySQL
/// refuses a connection whose default database does not yet exist — then creates the schema (a MySQL
/// <c>SCHEMA</c> is a database) and the <c>MIGRATIONS</c> control table, and applies the pending versions,
/// recording each one as it succeeds.
/// <para>
/// Two MySQL differences shape this implementation versus the Postgres runner:
/// <list type="bullet">
/// <item>MySQL has no transactional advisory lock, so concurrent runners for the same schema are
/// serialized with a named lock (<c>GET_LOCK</c>/<c>RELEASE_LOCK</c>), session-scoped, released in a
/// <c>finally</c>.</item>
/// <item>MySQL DDL causes an implicit commit and cannot be rolled back, so there is no enclosing
/// transaction. Each version is recorded in <c>MIGRATIONS</c> immediately after its script runs, so a
/// failure mid-batch leaves the applied versions recorded and a re-run resumes from the next pending one.</item>
/// </list>
/// </para>
/// </summary>
public sealed class MySqlMigrationRunner : IAxisMigrationRunner
{
    // GET_LOCK names are capped at 64 chars; a schema name is always well within that.
    private const int LockTimeoutSeconds = 30;

    public async Task RunAsync(string connectionString, string schema, (string Version, string Script)[] migrations)
    {
        var migrationsTable = $"{schema}.MIGRATIONS";

        // A migration runner must be able to bootstrap on a bare server, but MySQL refuses a connection
        // whose default database does not yet exist (ERROR 1049). So connect at the server level (no default
        // database) and create everything from there; every schema/table below is fully qualified, so the
        // runner needs no default database of its own.
        var builder = new MySqlConnectionStringBuilder(connectionString);
        var connectDatabase = builder.Database;
        builder.Database = string.Empty;

        await using var conn = new MySqlConnection(builder.ConnectionString);
        await conn.OpenAsync();

        // Idempotent bootstrap: the connection's own database (when it names one, so the application can
        // later open connections against it), this schema (a MySQL SCHEMA is a database), and the control table.
        var connectDatabaseDdl = !string.IsNullOrEmpty(connectDatabase) && !string.Equals(connectDatabase, schema, StringComparison.OrdinalIgnoreCase)
            ? $"CREATE SCHEMA IF NOT EXISTS {connectDatabase};"
            : string.Empty;

        await using (var cmd = new MySqlCommand(
                         connectDatabaseDdl +
                         $"CREATE SCHEMA IF NOT EXISTS {schema};" +
                         $"CREATE TABLE IF NOT EXISTS {migrationsTable} " +
                         "(VERSION VARCHAR(50) PRIMARY KEY, APPLIED_AT DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6))", conn))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        // Serializes concurrent migrations for the same schema (multiple instances running at once).
        // Session-scoped named lock — explicitly released in the finally below.
        await using (var lockCmd = new MySqlCommand("SELECT GET_LOCK(@name, @timeout)", conn))
        {
            lockCmd.Parameters.AddWithValue("name", schema);
            lockCmd.Parameters.AddWithValue("timeout", LockTimeoutSeconds);
            var acquired = await lockCmd.ExecuteScalarAsync();
            if (acquired is not 1L)
                throw new InvalidOperationException($"Could not acquire the MySQL migration lock for schema '{schema}' within {LockTimeoutSeconds}s.");
        }

        try
        {
            foreach (var (version, script) in migrations)
            {
                await using (var checkCmd = new MySqlCommand($"SELECT 1 FROM {migrationsTable} WHERE VERSION = @version", conn))
                {
                    checkCmd.Parameters.AddWithValue("version", version);
                    if (await checkCmd.ExecuteScalarAsync() is not null)
                        continue;
                }

                await using (var migCmd = new MySqlCommand(script, conn))
                    await migCmd.ExecuteNonQueryAsync();

                await using var recordCmd = new MySqlCommand($"INSERT INTO {migrationsTable} (VERSION) VALUES (@version)", conn);
                recordCmd.Parameters.AddWithValue("version", version);
                await recordCmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await using var unlockCmd = new MySqlCommand("SELECT RELEASE_LOCK(@name)", conn);
            unlockCmd.Parameters.AddWithValue("name", schema);
            await unlockCmd.ExecuteScalarAsync();
        }
    }
}
