using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisSaga.MySql.IntegrationTests;

// Exercises the dialect-agnostic IAxisSagaSettingsStore against a real MySQL AXIS_SAGA.SAGA_SETTINGS row —
// the SAME store class the Postgres suite drives, proving the single ADO.NET implementation serves both
// dialects. Each test controls the single settings row through InitializeAsync (seeded default) and leaves it
// unbounded in DisposeAsync so it can't gate the other tests that share this container's collection.
[Collection("AxisSagaMySqlCollection")]
public class AxisSagaSettingsStoreTests(AxisSagaMySqlFixture fixture) : IAsyncLifetime
{
    private const int SeededCap = 20;

    public async ValueTask InitializeAsync() => await WriteCapAsync(SeededCap);
    public async ValueTask DisposeAsync() => await WriteCapAsync(null);

    private IAxisSagaSettingsStore BuildStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<AxisSagaSettingsStore>>());
        services.AddAxisSagaMySql(new AxisSagaSettings
        {
            ConnectionString = fixture.ConnectionString,
            ResumerEnabled = false,
        });
        return services.BuildServiceProvider().GetRequiredService<IAxisSagaSettingsStore>();
    }

    [Fact]
    public async Task GetReadsTheCurrentCapAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = BuildStore();

        (await store.GetMaxConcurrentSagasAsync(ct)).ShouldSucceedWith(SeededCap);
    }

    [Fact]
    public async Task SetOverwritesTheCapAndNullMakesItUnboundedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = BuildStore();

        (await store.SetMaxConcurrentSagasAsync(75, ct)).ShouldSucceed();
        (await store.GetMaxConcurrentSagasAsync(ct)).ShouldSucceedWith(75);
        Assert.Equal(75, await ReadCapAsync());

        (await store.SetMaxConcurrentSagasAsync(null, ct)).ShouldSucceed();
        (await store.GetMaxConcurrentSagasAsync(ct)).ShouldSucceedWith(null);
        Assert.Null(await ReadCapAsync());
    }

    [Fact]
    public async Task TrySetRaisesOnlyWhileTheGuardMatchesAndNeverClobbersAManualTuneAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = BuildStore(); // InitializeAsync seeded the cap at 20

        // Guard matches the seeded value → raised.
        (await store.TrySetMaxConcurrentSagasAsync(SeededCap, 75, ct)).ShouldSucceedWith(true);
        Assert.Equal(75, await ReadCapAsync());

        // Guard no longer matches (still expecting 20, but it is 75 now) → no-op, value preserved.
        (await store.TrySetMaxConcurrentSagasAsync(SeededCap, 200, ct)).ShouldSucceedWith(false);
        Assert.Equal(75, await ReadCapAsync());

        // A value tuned by hand (33) is never overwritten by a raise guarded on the seed.
        await WriteCapAsync(33);
        (await store.TrySetMaxConcurrentSagasAsync(SeededCap, 75, ct)).ShouldSucceedWith(false);
        Assert.Equal(33, await ReadCapAsync());
    }

    [Fact]
    public async Task SetRejectsNonPositiveCapAndLeavesTheRowUntouchedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var store = BuildStore();

        (await store.SetMaxConcurrentSagasAsync(0, ct)).ShouldFailWithCode(AxisSagaErrors.InvalidConcurrencyCap);

        Assert.Equal(SeededCap, await ReadCapAsync());
    }

    // ─────────────────────────── helpers ───────────────────────────

    private async Task<int?> ReadCapAsync()
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"SELECT {SagaSettingsTable.MaxConcurrentSagas} FROM {SagaSettingsTable.Table} LIMIT 1", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    // UPSERT so it works whether or not the seed ran; NULL means unbounded.
    private async Task WriteCapAsync(int? cap)
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"""
             INSERT INTO {SagaSettingsTable.Table} ({SagaSettingsTable.OnlyRow}, {SagaSettingsTable.MaxConcurrentSagas})
             VALUES (1, @cap)
             ON DUPLICATE KEY UPDATE {SagaSettingsTable.MaxConcurrentSagas} = @cap
             """, conn);
        cmd.Parameters.AddWithValue("cap", (object?)cap ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
}
