using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Persistence.Scripts;
using Axis.Saga;
using Axis.SharedKernel;
using AxisSaga.Postgres.Adapters;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class SagaLeaseConcurrencyTests(AxisSagaPostgresFixture fixture)
{
    public sealed record ConcurrentPayload
    {
        public string Marker { get; init; } = "";
    }

    // Counts how many times the stage actually executes across all racing runs. The lease must keep this
    // at exactly one even when two engine runs target the same saga at the same time.
    public sealed class CountingStageHandler : IAxisSagaStageHandler<ConcurrentPayload>
    {
        public static int InvocationCount;
        public static void Reset() => Interlocked.Exchange(ref InvocationCount, 0);

        public string SagaName => "ConcurrentSaga";
        public string StageName => "OnlyStage";

        public async Task<AxisResult<ConcurrentPayload>> ExecuteAsync(ConcurrentPayload payload)
        {
            Interlocked.Increment(ref InvocationCount);
            // Hold the stage briefly so a racing run reaches AcquireLease while this run owns the lease,
            // exercising the "lease held -> skip" path rather than only the "already terminal -> skip" one.
            await Task.Delay(500);
            return AxisResult.Ok(payload);
        }
    }

    private IServiceProvider BuildSp()
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
        });
        services.AddScoped<IAxisSagaStageHandler<ConcurrentPayload>, CountingStageHandler>();
        services.AddSingleton(AxisSagaDefinitions.Define<ConcurrentPayload>("ConcurrentSaga", saga =>
        {
            saga.AddStage("OnlyStage").FinishOnSuccess();
        }));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TwoConcurrentRunsOfSameSagaExecuteStageOnceAsync()
    {
        CountingStageHandler.Reset();
        var sp = BuildSp();
        var engine = sp.GetRequiredService<SagaEngine>();

        var sagaId = $"lease-conc-{Guid.NewGuid():N}";
        await InsertPendingAsync(sagaId, "ConcurrentSaga");

        // Two engine runs race for the same saga. The lease lets exactly one acquire and execute the
        // stage; the other finds the lease held (or the saga already terminal) and no-ops.
        await Task.WhenAll(engine.ExecuteAsync(sagaId), engine.ExecuteAsync(sagaId));

        Assert.Equal(1, CountingStageHandler.InvocationCount);
        Assert.Equal(nameof(AxisSagaStatus.Completed), await StatusAsync(sagaId));
    }

    private async Task InsertPendingAsync(string sagaId, string sagaName)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"""
             INSERT INTO {SagaInstancesTable.Table}
                 ({SagaInstancesTable.SagaId}, {SagaInstancesTable.SagaName}, {SagaInstancesTable.Status},
                  {SagaInstancesTable.PayloadJson}, {SagaInstancesTable.Version})
             VALUES (@id, @name, @status, @payload::JSONB, 1)
             """, conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        cmd.Parameters.AddWithValue("name", sagaName);
        cmd.Parameters.AddWithValue("status", nameof(AxisSagaStatus.Pending));
        cmd.Parameters.AddWithValue("payload", "{}");
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string?> StatusAsync(string sagaId)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT {SagaInstancesTable.Status} FROM {SagaInstancesTable.Table} WHERE {SagaInstancesTable.SagaId} = @id",
            conn);
        cmd.Parameters.AddWithValue("id", sagaId);
        return (await cmd.ExecuteScalarAsync()) as string;
    }
}
