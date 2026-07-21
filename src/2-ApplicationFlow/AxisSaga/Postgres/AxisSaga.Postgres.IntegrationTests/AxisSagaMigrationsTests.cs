using AxisSaga.Postgres.Persistence;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class AxisSagaMigrationsTests(AxisSagaPostgresFixture fixture)
{
    [Fact]
    public async Task InitializePostgresAsync_ShouldBeIdempotent()
    {
        // Run again — fixture already ran it once. No throw means OK.
        await AxisSagaMigrations.InitializePostgresAsync(fixture.ConnectionString);
        await AxisSagaMigrations.InitializePostgresAsync(fixture.ConnectionString);
    }

    [Fact]
    public async Task InitializePostgresAsync_ShouldCreateAllTables()
    {
        await AxisSagaMigrations.InitializePostgresAsync(fixture.ConnectionString);
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        // Postgres stores unquoted identifiers in lowercase
        var tables = new[] { "saga_instances", "saga_stage_logs", "saga_definitions", "migrations" };
        foreach (var table in tables)
        {
            await using var cmd = new NpgsqlCommand("SELECT 1 FROM information_schema.tables WHERE table_schema = 'axis_saga' AND table_name = @t", conn);
            cmd.Parameters.AddWithValue("t", table);
            var result = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(result);
        }
    }
}
