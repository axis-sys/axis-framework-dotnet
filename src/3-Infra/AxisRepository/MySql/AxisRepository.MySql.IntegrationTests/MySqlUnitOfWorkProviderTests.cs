using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AxisRepository.MySql.IntegrationTests;

// Mirrors PostgresUnitOfWorkProviderTests: AddMySqlUnitOfWork wires the keyed data source + provider behind
// both IMySqlUnitOfWork and IAxisUnitOfWork, and the provider hands back one cached unit of work per key.
[Collection("AxisRepositoryMySqlCollection")]
public class MySqlUnitOfWorkProviderTests(MySqlFixture fixture)
{
    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddAxisLogger();
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
        services.AddSingleton(TimeProvider.System);

        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        mediator.SetupGet(x => x.TraceId).Returns("trace");
        mediator.SetupGet(x => x.OriginId).Returns("origin");
        mediator.SetupGet(x => x.JourneyId).Returns((string?)null);
        services.AddSingleton(mediator.Object);

        services.AddMySqlUnitOfWork("sandbox", fixture.ConnectionString);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetUnitOfWorkReturnsCachedInstancePerKey()
    {
        var sp = Build();
        var provider = sp.GetRequiredKeyedService<MySqlUnitOfWorkProvider>("sandbox");

        var first = provider.GetUnitOfWork(sp, "sandbox");
        var second = provider.GetUnitOfWork(sp, "sandbox");

        Assert.Same(first, second);
    }

    [Fact]
    public async Task DependencyInjectionResolvesIMySqlUnitOfWorkAndIAxisUnitOfWork()
    {
        var sp = Build();
        using var scope = sp.CreateScope();

        var mysql = scope.ServiceProvider.GetRequiredKeyedService<IMySqlUnitOfWork>("sandbox");
        var axis = scope.ServiceProvider.GetRequiredKeyedService<IAxisUnitOfWork>("sandbox");

        var start = await mysql.StartAsync();
        start.ShouldSucceed();
        await axis.RollbackAsync();
    }
}
