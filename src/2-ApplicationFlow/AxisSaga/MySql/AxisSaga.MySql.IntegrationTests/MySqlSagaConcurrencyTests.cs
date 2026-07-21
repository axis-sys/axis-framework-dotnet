using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.MySql.Adapters;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace AxisSaga.MySql.IntegrationTests;

/// <summary>
/// Concurrency guarantees of the MySQL adapter: the connection-less LEASE keeps two racing engine runs
/// from executing the same stage twice (exercising the RETURNING-less UPDATE+SELECT claim), and the
/// global cap in SAGA_SETTINGS gates the atomic claim (exercising the derived-table live-lease COUNT
/// that works around MySQL error 1093).
/// </summary>
[Collection("AxisSagaMySqlCollection")]
public class MySqlSagaConcurrencyTests(AxisSagaMySqlFixture fixture) : IAsyncLifetime
{
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    // Leave the shared SAGA_SETTINGS unbounded after each test so it can't gate other tests' sagas.
    public async ValueTask DisposeAsync() => await SetCapAsync(null);

    public sealed record ConcurrentPayload
    {
        public string Marker { get; init; } = "";
    }

    public sealed class CountingStageHandler : IAxisSagaStageHandler<ConcurrentPayload>
    {
        public static int InvocationCount;
        public static void Reset() => Interlocked.Exchange(ref InvocationCount, 0);

        public string SagaName => "MySqlConcurrentSaga";
        public string StageName => "OnlyStage";

        public async Task<AxisResult<ConcurrentPayload>> ExecuteAsync(ConcurrentPayload payload)
        {
            Interlocked.Increment(ref InvocationCount);
            await Task.Delay(500);
            return AxisResult.Ok(payload);
        }
    }

    [Fact]
    public async Task TwoConcurrentRunsOfSameSagaExecuteStageOnceAsync()
    {
        CountingStageHandler.Reset();
        var sp = BuildSp(services =>
        {
            services.AddScoped<IAxisSagaStageHandler<ConcurrentPayload>, CountingStageHandler>();
            services.AddSingleton(AxisSagaDefinitions.Define<ConcurrentPayload>("MySqlConcurrentSaga", saga =>
                saga.AddStage("OnlyStage").FinishOnSuccess()));
        });
        var engine = sp.GetRequiredService<SagaEngine>();

        var sagaId = $"lease-conc-{Guid.NewGuid():N}";
        await InsertPendingAsync(sagaId, "MySqlConcurrentSaga");

        await Task.WhenAll(engine.ExecuteAsync(sagaId), engine.ExecuteAsync(sagaId));

        Assert.Equal(1, CountingStageHandler.InvocationCount);
        Assert.Equal(nameof(AxisSagaStatus.Completed), await StatusAsync(sagaId));
    }

    [Fact]
    public async Task AcquireLeaseDeniesAtCapAndAdmitsWhenSlotFreesAsync()
    {
        await ClearAsync();
        await SetCapAsync(2);
        var sp = BuildSp(_ => { });
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        await InsertSagaAsync("live1", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "owner1");
        await InsertSagaAsync("live2", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), "owner2");
        await InsertSagaAsync("p3", AxisSagaStatus.Pending, null);

        // Gate full → claim denied (null, exactly like a lease held by another run).
        Assert.Null(await store.AcquireLeaseAsync("p3", "runner3", 300));

        // Free one slot → the claim now succeeds.
        await ExpireLeaseAsync("live1");
        Assert.NotNull(await store.AcquireLeaseAsync("p3", "runner3", 300));
    }

    [Fact]
    public async Task AcquireLeaseWithNullCapIsUnboundedAsync()
    {
        await ClearAsync();
        await SetCapAsync(null);
        var sp = BuildSp(_ => { });
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        for (var i = 0; i < 5; i++)
            await InsertSagaAsync($"live{i}", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), $"o{i}");
        await InsertSagaAsync("p", AxisSagaStatus.Pending, null);

        Assert.NotNull(await store.AcquireLeaseAsync("p", "runner", 300));
    }

    /// <summary>
    /// Regression for the InnoDB deadlock storm under the import fan-out: many engine runs claim leases
    /// while new sagas are inserted at the same time. The lease claim gates on a COUNT over the live
    /// leases inside its UPDATE; under InnoDB's default REPEATABLE READ that scan takes next-key/gap
    /// locks that deadlock racing claims and block concurrent INSERTs (error 1213). The adapter pins the
    /// store connections to READ COMMITTED (no gap locks) and retries the transient lock errors, so every
    /// claim and insert must succeed here. Before the fix the swallowed deadlocks surface as spurious
    /// null claims and failed inserts.
    /// </summary>
    [Fact]
    public async Task HighConcurrencyClaimsAndInsertsDoNotDeadlockAsync()
    {
        await ClearAsync();
        // Non-null cap forces the live-lease COUNT scan (the deadlock vector); high enough to admit all.
        await SetCapAsync(1000);
        var sp = BuildSp(_ => { });
        var store = sp.GetRequiredService<ISagaInstanceStore>();

        // Seed live leases so the cap-check COUNT scans a non-trivial range.
        for (var i = 0; i < 40; i++)
            await InsertSagaAsync($"seed-{i}", AxisSagaStatus.Running, TimeSpan.FromMinutes(5), $"seed-owner-{i}");

        const int rounds = 5;
        const int concurrency = 30;
        for (var round = 0; round < rounds; round++)
        {
            var pendingIds = Enumerable.Range(0, concurrency).Select(i => $"pending-{round}-{i}").ToArray();
            foreach (var id in pendingIds)
                await InsertPendingAsync(id, "MySqlCapSaga");

            // Race: claim every pending saga while inserting brand-new sagas at the same time.
            var claimTasks = pendingIds.Select(id => store.AcquireLeaseAsync(id, $"runner-{id}", 300)).ToArray();
            var insertTasks = Enumerable.Range(0, concurrency)
                .Select(i => store.InsertAsync($"insert-{round}-{i}", "MySqlCapSaga", "{}", null)).ToArray();

            var claimed = await Task.WhenAll(claimTasks);
            var inserted = await Task.WhenAll(insertTasks);

            Assert.All(claimed, instance => Assert.NotNull(instance));
            Assert.All(inserted, result => result.ShouldSucceed());
        }
    }

    private IServiceProvider BuildSp(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<MySqlSagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddAxisSagaMySql(new AxisSagaSettings
        {
            ConnectionString = fixture.ConnectionString,
            ResumerEnabled = false,
            ResumeAfter = TimeSpan.FromMinutes(5),
        });
        configure(services);
        return services.BuildServiceProvider();
    }

    // ─────────────────────────── raw MySQL helpers ───────────────────────────

    private async Task InsertPendingAsync(string sagaId, string sagaName)
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"""
             INSERT INTO {SagaInstancesTable.Table}
                 ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                  {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version})
             VALUES (@id, @name, @status, @payload, 1)
             """, conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        cmd.Parameters.AddWithValue("name", sagaName);
        cmd.Parameters.AddWithValue("status", nameof(AxisSagaStatus.Pending));
        cmd.Parameters.AddWithValue("payload", "{}");
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task InsertSagaAsync(string sagaId, AxisSagaStatus status, TimeSpan? claimedUntil, string? claimedBy = null)
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"""
             INSERT INTO {SagaInstancesTable.Table}
                 ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                  {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version},
                  {SagaInstancesTable.ClaimedBy}, {SagaInstancesTable.ClaimedUntil})
             VALUES (@id, @name, @status, @payload, 1, @claimedBy,
                     CASE WHEN @hasLease THEN UTC_TIMESTAMP(6) + INTERVAL @leaseSecs SECOND ELSE NULL END)
             """, conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        cmd.Parameters.AddWithValue("name", "MySqlCapSaga");
        cmd.Parameters.AddWithValue("status", status.ToString());
        cmd.Parameters.AddWithValue("payload", "{}");
        cmd.Parameters.AddWithValue("claimedBy", (object?)claimedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hasLease", claimedUntil.HasValue);
        cmd.Parameters.AddWithValue("leaseSecs", claimedUntil.HasValue ? (int)claimedUntil.Value.TotalSeconds : 0);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SetCapAsync(int? cap)
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

    private async Task ClearAsync()
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand($"DELETE FROM {SagaInstancesTable.Table}", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task ExpireLeaseAsync(string sagaId)
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"UPDATE {SagaInstancesTable.Table} SET {SagaInstancesTable.ClaimedUntil} = UTC_TIMESTAMP(6) - INTERVAL 1 SECOND WHERE {SagaInstancesTable.SagaId} = @id",
            conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string?> StatusAsync(string sagaId)
    {
        await using var conn = new MySqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new MySqlCommand(
            $"SELECT {SagaInstancesTable.Status} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id",
            conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        return (await cmd.ExecuteScalarAsync()) as string;
    }
}
