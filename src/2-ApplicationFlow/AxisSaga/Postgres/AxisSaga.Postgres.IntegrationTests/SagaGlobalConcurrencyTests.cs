using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

// Verifies the GLOBAL concurrency cap stored in AXIS_SAGA.SAGA_SETTINGS.MAX_CONCURRENT_SAGAS: a soft,
// distributed limit on how many sagas hold a live lease at once, enforced inside the atomic lease claim.
// The cap is a single row read straight from the database, so multiple concurrent engine runs in this one
// process faithfully model multiple pods sharing one database. Each test clears SAGA_INSTANCES first (the
// cap counts live leases GLOBALLY) and sets the cap explicitly; the gate tests pre-seed live leases to
// assert the gate deterministically (no reliance on burst timing, which a soft cap may transiently
// overshoot). DisposeAsync resets the cap to unbounded so it never leaks into the other tests that share
// the container.
[Collection("AxisSagaPostgresCollection")]
public class SagaGlobalConcurrencyTests(AxisSagaPostgresFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Leave the shared SAGA_SETTINGS unbounded after each test so it can't gate the other tests' sagas.
    public async ValueTask DisposeAsync() => await SetCapAsync(null);

    public sealed record CapPayload
    {
        public string Marker { get; init; } = "";
    }

    // A registered saga whose single stage completes quickly, so sagas deferred by the cap can be driven
    // to completion by the resumer.
    public sealed class FastStageHandler : IAxisSagaStageHandler<CapPayload>
    {
        public string SagaName => "CapSaga";
        public string StageName => "OnlyStage";

        public async Task<AxisResult<CapPayload>> ExecuteAsync(CapPayload payload)
        {
            await Task.Delay(50);
            return AxisResult.Ok(payload);
        }
    }

    private IServiceProvider BuildSp(int? batchSize = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddAxisSagaPostgres(new AxisSagaSettings
        {
            ConnectionString = fixture.ConnectionString,
            ResumeAfter = TimeSpan.FromMinutes(5),
            ResumeBatchSize = batchSize ?? 100,
        });
        services.AddScoped<IAxisSagaStageHandler<CapPayload>, FastStageHandler>();
        services.AddSingleton(AxisSagaDefinitions.Define<CapPayload>("CapSaga", saga =>
        {
            saga.AddStage("OnlyStage").FinishOnSuccess();
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task AcquireLeaseDeniesAtCapAndAdmitsWhenSlotFreesAsync()
    {
        await ClearAsync();
        await SetCapAsync(2);
        var sp = BuildSp();
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        // Two sagas already executing (live lease) fill the two slots.
        await InsertSagaAsync("live1", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "owner1");
        await InsertSagaAsync("live2", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "owner2");
        // A third saga waiting to start.
        await InsertSagaAsync("p3", AxisSagaStatus.Pending, null);

        // Gate full → claim denied (null, exactly like a lease held by another run).
        Assert.Null(await store.AcquireLeaseAsync("p3", "runner3", 300));

        // Free one slot → the claim now succeeds.
        await ExpireLeaseAsync("live1");
        Assert.NotNull(await store.AcquireLeaseAsync("p3", "runner3", 300));
    }

    [Fact]
    public async Task AcquireLeaseCountsByLeaseLivenessNotStatusAsync()
    {
        // A claimed-but-still-Pending saga (live lease, STATUS = Pending) MUST count toward the cap — this
        // is the window between AcquireLease and the first stage flipping the status to Running. A
        // status-based count would miss it and let the cap be exceeded.
        await ClearAsync();
        await SetCapAsync(1);
        var sp = BuildSp();
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        await InsertSagaAsync("pendingLeased", AxisSagaStatus.Pending, TimeSpan.FromMinutes(5), "owner");
        await InsertSagaAsync("p2", AxisSagaStatus.Pending, null);

        // The single live lease (held by a still-Pending saga) already fills the cap of 1.
        Assert.Null(await store.AcquireLeaseAsync("p2", "runner2", 300));
    }

    [Fact]
    public async Task AcquireLeaseWithNullCapIsUnboundedAsync()
    {
        await ClearAsync();
        await SetCapAsync(null);
        var sp = BuildSp();
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        for (var i = 0; i < 5; i++)
            await InsertSagaAsync($"live{i}", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), $"o{i}");
        await InsertSagaAsync("p", AxisSagaStatus.Pending, null);

        // No cap → admitted regardless of how many sagas are live.
        Assert.NotNull(await store.AcquireLeaseAsync("p", "runner", 300));
    }

    [Fact]
    public async Task ResumerFetchesAtMostFreeSlotsAsync()
    {
        await ClearAsync();
        await SetCapAsync(2);
        var sp = BuildSp();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        // Two live leases fill the cap; three pending wait. (UnregisteredSaga → the fired engine cannot
        // complete them, but we assert the resumer's claim count, which is sized to free slots.)
        await InsertSagaAsync("live1", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "o1");
        await InsertSagaAsync("live2", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "o2");
        for (var i = 0; i < 3; i++)
            await InsertSagaAsync($"p{i}", AxisSagaStatus.Pending, null, sagaName: "UnregisteredSaga");

        // Gate full → resumer fetches nothing (no doomed fire-and-forget dispatches).
        Assert.Equal(0, await resumer.RunOnceAsync(CancellationToken.None));

        // Free one slot → resumer fetches exactly one.
        await ExpireLeaseAsync("live2");
        Assert.Equal(1, await resumer.RunOnceAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ExcessSagasDeferredByCapAllCompleteViaResumerAsync()
    {
        const int cap = 2;
        const int total = 6;
        await ClearAsync();
        await SetCapAsync(cap);
        var sp = BuildSp();
        var engine = sp.GetRequiredService<SagaEngine>();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var ids = new List<string>();
        for (var i = 0; i < total; i++)
        {
            var id = $"d{i}";
            ids.Add(id);
            await InsertSagaAsync(id, AxisSagaStatus.Pending, null, sagaName: "CapSaga");
        }

        // Fire them all at once: at most ~cap pass the gate; the rest stay Pending (the cap defers, never
        // drops). Then the resumer drives the deferred ones to completion as slots free.
        await Task.WhenAll(ids.Select(id => engine.ExecuteAsync(id)));

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline && await CountByStatusAsync(nameof(AxisSagaStatus.Completed)) < total)
        {
            await resumer.RunOnceAsync(CancellationToken.None);
            await Task.Delay(200, TestContext.Current.CancellationToken);
        }

        Assert.Equal(total, await CountByStatusAsync(nameof(AxisSagaStatus.Completed)));
    }

    // ─────────────────────────── helpers ───────────────────────────

    // Sets the global cap (the single SAGA_SETTINGS row). UPSERT so it works whether or not the seed ran.
    private async Task SetCapAsync(int? cap)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"""
             INSERT INTO {SagaSettingsTable.Table} ({SagaSettingsTable.OnlyRow}, {SagaSettingsTable.MaxConcurrentSagas})
             VALUES (TRUE, @cap)
             ON CONFLICT ({SagaSettingsTable.OnlyRow}) DO UPDATE SET {SagaSettingsTable.MaxConcurrentSagas} = @cap
             """, conn);
        cmd.Parameters.AddWithValue("cap", (object?)cap ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // The global cap counts live leases across the whole table, so each test starts from an empty table to
    // keep leftover leases from other tests in the shared container out of the count.
    private async Task ClearAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DELETE FROM {SagaInstancesTable.Table}", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertSagaAsync(
        string sagaId, AxisSagaStatus status, TimeSpan? claimedUntil, string? claimedBy = null, string sagaName = "CapSaga")
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"""
             INSERT INTO {SagaInstancesTable.Table}
                 ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                  {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version},
                  {SagaInstancesTable.ClaimedBy}, {SagaInstancesTable.ClaimedUntil})
             VALUES (@id, @name, @status, @payload::JSONB, 1, @claimedBy,
                     CASE WHEN @hasLease THEN NOW() + @lease ELSE NULL END)
             """, conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        cmd.Parameters.AddWithValue("name", sagaName);
        cmd.Parameters.AddWithValue("status", status.ToString());
        cmd.Parameters.AddWithValue("payload", "{}");
        cmd.Parameters.AddWithValue("claimedBy", (object?)claimedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hasLease", claimedUntil.HasValue);
        cmd.Parameters.AddWithValue("lease", claimedUntil ?? TimeSpan.Zero);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExpireLeaseAsync(string sagaId)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {SagaInstancesTable.Table} SET {SagaInstancesTable.ClaimedUntil} = NOW() - make_interval(secs => 1) WHERE {SagaInstancesTable.SagaId} = @id",
            conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<int> CountByStatusAsync(string status)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT COUNT(*) FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.Status} = @status", conn);
        cmd.Parameters.AddWithValue("status", status);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
