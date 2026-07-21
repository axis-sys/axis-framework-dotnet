using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace AxisRepository.Postgres.IntegrationTests;

[Collection("AxisRepositoryPostgresCollection")]
public class PostgresUnitOfWorkProviderTests(PostgresFixture fixture)
{
    private IServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddAxisLogger();
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IAxisMediatorContextAccessor, StubContextAccessor>();
        services.AddScoped<IAxisMediatorAccessor, StubAccessor>();
        services.AddScoped<IAxisMediator>(_ => new StubMediator());

        services.AddPostgresUnitOfWork("sandbox", fixture.ConnectionString);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetUnitOfWorkReturnsCachedInstancePerKey()
    {
        var sp = Build();
        var provider = sp.GetRequiredKeyedService<PostgresUnitOfWorkProvider>("sandbox");

        var first = provider.GetUnitOfWork(sp, "sandbox");
        var second = provider.GetUnitOfWork(sp, "sandbox");

        Assert.Same(first, second);
    }

    [Fact]
    public async Task DependencyInjectionResolvesIPostgresUnitOfWorkAndIAxisUnitOfWork()
    {
        var sp = Build();
        using var scope = sp.CreateScope();

        var pg = scope.ServiceProvider.GetRequiredKeyedService<IPostgresUnitOfWork>("sandbox");
        var axis = scope.ServiceProvider.GetRequiredKeyedService<IAxisUnitOfWork>("sandbox");

        var start = await pg.StartAsync();
        start.ShouldSucceed();
        await axis.RollbackAsync();
    }

    private sealed class StubContextAccessor : IAxisMediatorContextAccessor
    {
        public string? OriginId { get; set; } = "t";
        public string? JourneyId { get; set; }
        public AxisEntityId? AxisEntityId { get; set; }
        public CancellationToken CancellationToken { get; set; }
    }

    private sealed class StubAccessor : IAxisMediatorAccessor
    {
        public IAxisMediator? AxisMediator { get; set; }
    }

    private sealed class StubMediator : IAxisMediator
    {
        public CancellationToken CancellationToken => CancellationToken.None;
        public string TraceId => "t";
        public string OriginId => "o";
        public string? JourneyId => null;
        public AxisEntityId? AxisEntityId => null;
        public IAxisMediatorHandler Cqrs => null!;
    }
}
