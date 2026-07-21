using AxisSaga.MySql.Persistence;
using MySqlConnector;

namespace AxisSaga.MySql.IntegrationTests;

[Collection("AxisSagaMySqlCollection")]
public class AxisSagaMySqlMigrationsTests(AxisSagaMySqlFixture fixture)
{
    [Fact]
    public async Task InitializeMySqlAsync_ShouldBeIdempotent()
    {
        // Run again — fixture already ran it once. No throw means OK.
        await AxisSagaMySqlMigrations.InitializeMySqlAsync(fixture.ConnectionString);
        await AxisSagaMySqlMigrations.InitializeMySqlAsync(fixture.ConnectionString);
    }

    [Fact]
    public async Task InitializeMySqlAsync_ShouldCreateAllTables()
    {
        await AxisSagaMySqlMigrations.InitializeMySqlAsync(fixture.ConnectionString);
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(TestContext.Current.CancellationToken);

        var tables = new[] { "SAGA_INSTANCES", "SAGA_STAGE_LOGS", "SAGA_DEFINITIONS", "SAGA_SETTINGS", "MIGRATIONS" };
        foreach (var table in tables)
        {
            await using var cmd = new MySqlCommand(
                "SELECT 1 FROM information_schema.tables WHERE table_schema = 'AXIS_SAGA' AND table_name = @t", conn);
            cmd.Parameters.AddWithValue("t", table);
            var result = await cmd.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(result);
        }
    }
}
