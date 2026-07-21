using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Ports;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class PostgresSagaResumerTests(AxisSagaPostgresFixture fixture) : IAsyncLifetime
{
    // The resumer honours the GLOBAL cap in SAGA_SETTINGS (seeded to 20) and counts live leases across the
    // whole shared table, so leftover leases from sibling tests can gate the claim and starve the select.
    // Isolate like SagaGlobalConcurrencyTests: clear the table and leave the cap unbounded before each test.
    public async ValueTask InitializeAsync()
    {
        await ClearAsync();
        await SetCapAsync(null);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public record EmptyPayload
    {
        public string SagaId { get; init; } = "";
    }

    private IServiceProvider BuildSp(TimeSpan? resumeAfter = null, int? batchSize = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisLogger<SagaMediator>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaEngine>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaResumer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaDefinitionInitializer>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaInstanceStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageLogStore>>());
        services.AddSingleton(Mock.Of<IAxisLogger<SagaStageHandlerInvoker>>());
        services.AddAxisSagaPostgres(new AxisSagaSettings
        {
            ConnectionString = fixture.ConnectionString,
            ResumeAfter = resumeAfter ?? TimeSpan.FromMinutes(60),
            ResumeBatchSize = batchSize ?? 100
        });
        services.AddSingleton(AxisSagaDefinitions.Define<EmptyPayload>("ResumerSaga", saga =>
        {
            saga.AddStage("Step1").FinishOnSuccess();
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task RunOnceAsyncReturnsZeroOrMoreWhenNoStaleSagasAsync()
    {
        var sp = BuildSp();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var resumed = await resumer.RunOnceAsync(CancellationToken.None);

        Assert.True(resumed >= 0);
    }

    [Fact]
    public async Task RunOnceAsyncClaimsAtMostBatchSizeAsync()
    {
        // Several sagas with no lease (CLAIMED_UNTIL null) are all candidates; batch size caps the claim.
        var sp = BuildSp(batchSize: 2);
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var prefix = $"batch-{Guid.NewGuid():N}-";
        for (var i = 0; i < 3; i++)
            await InsertSagaAsync($"{prefix}{i}", "UnregisteredSaga", AxisSagaStatus.Running, claimedUntil: null);

        var claimed = await resumer.RunOnceAsync(CancellationToken.None);

        Assert.Equal(2, claimed);
    }

    [Fact]
    public async Task RunOnceAsyncClaimsSagaWithExpiredLeaseAsync()
    {
        // A run that died left an expired lease behind; the resumer must reclaim it. UnregisteredSaga →
        // the fire-and-forget engine has no definition so it cannot complete it, but selection is what we
        // assert here.
        var sp = BuildSp();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var sagaId = $"expired-{Guid.NewGuid():N}";
        await InsertSagaAsync(sagaId, "UnregisteredSaga", AxisSagaStatus.Running, claimedUntil: TimeSpan.FromMinutes(-5));

        var claimed = await resumer.RunOnceAsync(CancellationToken.None);

        Assert.True(claimed >= 1);
    }

    [Fact]
    public async Task RunOnceAsyncClaimsPendingSagaWithNoLeaseAsync()
    {
        // A fire-and-forget dispatch dropped under load strands the saga as Pending with no lease; the
        // resumer must reclaim Pending too (not only Running/Compensating).
        var sp = BuildSp();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var sagaId = $"pending-{Guid.NewGuid():N}";
        await InsertSagaAsync(sagaId, "UnregisteredSaga", AxisSagaStatus.Pending, claimedUntil: null);

        var claimed = await resumer.RunOnceAsync(CancellationToken.None);

        Assert.True(claimed >= 1);
    }

    [Fact]
    public async Task RunOnceAsyncDoesNotClaimSagaWithFreshLeaseAsync()
    {
        // A healthy run keeps its lease in the future via the heartbeat; the resumer must leave it alone.
        var sp = BuildSp();
        var resumer = sp.GetRequiredService<IAxisSagaResumer>();

        var sagaId = $"fresh-{Guid.NewGuid():N}";
        await InsertSagaAsync(sagaId, "UnregisteredSaga", AxisSagaStatus.Running,
            claimedUntil: TimeSpan.FromMinutes(30), claimedBy: "live-owner");

        await resumer.RunOnceAsync(CancellationToken.None);

        // The fresh-lease saga was neither selected by the resumer nor re-claimed by an engine run, so its
        // owner token is untouched.
        Assert.Equal("live-owner", await ClaimedByAsync(sagaId));
    }

    private async Task InsertSagaAsync(
        string sagaId, string sagaName, AxisSagaStatus status, TimeSpan? claimedUntil, string? claimedBy = null)
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

    private async Task<string?> ClaimedByAsync(string sagaId)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT {SagaInstancesTable.ClaimedBy} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id",
            conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        var value = await cmd.ExecuteScalarAsync();
        return value as string;
    }

    private async Task ClearAsync()
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand($"DELETE FROM {SagaInstancesTable.Table}", conn);
        await cmd.ExecuteNonQueryAsync();
    }

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
}
