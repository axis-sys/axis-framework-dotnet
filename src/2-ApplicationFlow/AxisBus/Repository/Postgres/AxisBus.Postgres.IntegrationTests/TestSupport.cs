using AxisBus.Repository;
using AxisBus.Repository.Ports;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Events;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AxisBus.Postgres.IntegrationTests;

/// <summary>
/// Shared DI wiring and outbox helpers for every test class in this project. The fixture already ran the schema
/// migration, so tests disable both hosted workers and drive the dispatcher explicitly via
/// <c>IBusDispatcher.RunOnceAsync</c> — deterministic and fast, no background poll loop to race against. Rows
/// are written through the real write path (publish enqueues; the unit-of-work drain flushes at commit), and
/// asserted with direct SQL against <c>AXIS_OUTBOX.OUTBOX_EVENTS</c> (presence = pending; delivery = deletion).
/// </summary>
internal static class TestSupport
{
    public static ServiceProvider BuildProvider(
        this AxisBusPostgresFixture fixture,
        int? batchSize = null,
        Action<IServiceCollection>? configureServices = null)
    {
        ServiceCollection services = new();
        Mock<IAxisMediator> mediator = new();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        services.AddSingleton(mediator.Object);
        services.AddSingleton<IAxisMediatorAccessor>(new StubAccessor { AxisMediator = mediator.Object });
        services.AddAxisLogger();

        AxisBusRepositorySettings defaults = new() { ConnectionString = fixture.ConnectionString };
        AxisBusRepositorySettings settings = new()
        {
            ConnectionString = fixture.ConnectionString,
            RunStartupMigration = false,
            DispatcherEnabled = false,
            BatchSize = batchSize ?? defaults.BatchSize,
        };

        services.AddAxisBusPostgres(settings);
        configureServices?.Invoke(services);
        return services.BuildServiceProvider();
    }

    // The write path exactly as production runs it: publishing enqueues on the request-scoped queue, and the
    // unit of work's drain flushes that queue into ITS OWN transaction at commit. Publish and drain must share
    // one DI scope (the queue is scoped), so this owns the scope end to end.
    public static Task PublishAndDrainAsync<TEvent>(this ServiceProvider sp, TEvent @event, CancellationToken cancellationToken)
        where TEvent : IAxisEvent
        => sp.PublishAllAndDrainAsync([@event], cancellationToken);

    public static async Task PublishAllAndDrainAsync<TEvent>(this ServiceProvider sp, IEnumerable<TEvent> events, CancellationToken cancellationToken)
        where TEvent : IAxisEvent
    {
        await using var scope = sp.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IAxisBus>();
        foreach (var @event in events)
            (await bus.PublishAsync(@event)).ShouldSucceed();

        var drain = scope.ServiceProvider.GetRequiredService<IAxisRepositoryOutbox>();
        var connections = scope.ServiceProvider.GetRequiredService<IAxisBusConnectionFactory>();
        await using var conn = await connections.OpenConnectionAsync(cancellationToken);
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);
        (await drain.DrainAsync(conn, tx, cancellationToken)).ShouldSucceed();
        await tx.CommitAsync(cancellationToken);
    }

    public static async Task<int> CountRowsAsync(string connectionString, string eventType)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(
            "SELECT COUNT(*) FROM AXIS_OUTBOX.OUTBOX_EVENTS WHERE EVENT_TYPE = @eventType", conn);
        cmd.Parameters.AddWithValue("eventType", eventType);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    // The (single) row for an event type: Exists is false once it is delivered (deleted); a non-null ClaimedBy
    // means a lease is held, null means it was released (re-claimable on the next pass).
    public static async Task<(bool Exists, string? ClaimedBy)> ReadClaimAsync(string connectionString, string eventType)
    {
        await using NpgsqlConnection conn = new(connectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(
            "SELECT CLAIMED_BY FROM AXIS_OUTBOX.OUTBOX_EVENTS WHERE EVENT_TYPE = @eventType", conn);
        cmd.Parameters.AddWithValue("eventType", eventType);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (false, null);
        return (true, await reader.IsDBNullAsync(0) ? null : reader.GetString(0));
    }

    private sealed class StubAccessor : IAxisMediatorAccessor
    {
        public IAxisMediator? AxisMediator { get; set; }
    }
}
