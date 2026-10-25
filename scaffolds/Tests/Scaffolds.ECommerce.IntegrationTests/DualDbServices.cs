using Axis.Saga;
using AxisBus.MySql;
using AxisBus.Postgres;
using AxisBus.Repository;
using AxisCache.MySql;
using AxisCache.Postgres;
using AxisCache.Repository;
using AxisSaga.MySql;
using AxisSaga.Postgres;
using Microsoft.Extensions.Configuration;
using Scaffolds.ECommerce.Adapters.Driven.InMemory;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;

namespace Scaffolds.ECommerce.IntegrationTests;

internal static class DualDbServices
{
    public static ServiceProvider Build(DualDbFixture fixture, DbProvider provider)
    {
        var services = new ServiceCollection();
        var configuration = fixture.Configuration();
        services.AddSingleton(configuration);
        services.AddAxisLogger();

        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(m => m.CancellationToken).Returns(CancellationToken.None);
        mediator.SetupGet(m => m.TraceId).Returns("trace");
        mediator.SetupGet(m => m.OriginId).Returns("origin");
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);

        if (provider == DbProvider.MySql)
            services.AddECommerceMySql(configuration);
        else
            services.AddECommercePostgres(configuration);

        return services.BuildServiceProvider();
    }

    public static ServiceProvider BuildFullStack(
        DualDbFixture fixture, DbProvider provider, Action<IServiceCollection>? configure = null, string? sagaConnectionString = null)
    {
        var services = new ServiceCollection();
        var configuration = fixture.Configuration();
        var connectionString = fixture.ConnectionString(provider);
        services.AddSingleton(configuration);
        services.AddAxisLogger();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddECommerceApplication();
        services.AddECommerceFacade();
        services.AddInMemoryEmail();

        var bus = new AxisBusRepositorySettings { ConnectionString = connectionString };
        var saga = new AxisSagaSettings { ConnectionString = sagaConnectionString ?? connectionString };
        var cache = new AxisCacheRepositorySettings { ConnectionString = connectionString };
        if (provider == DbProvider.MySql)
            services.AddECommerceMySql(configuration).AddAxisBusMySql(bus).AddAxisCacheMySql(cache).AddAxisSagaMySql(saga);
        else
            services.AddECommercePostgres(configuration).AddAxisBusPostgres(bus).AddAxisCachePostgres(cache).AddAxisSagaPostgres(saga);

        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    public static ServiceProvider BuildAdministration(DbProvider provider, IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddSingleton(configuration);
        services.AddAxisLogger();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddECommerceApplication();
        services.AddECommerceFacade();

        if (provider == DbProvider.MySql)
            services.AddECommerceMySql(configuration);
        else
            services.AddECommercePostgres(configuration);

        return services.BuildServiceProvider();
    }
}
