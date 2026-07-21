using Axis.Saga;
using Microsoft.Extensions.DependencyInjection;

namespace AxisSaga.Postgres.IntegrationTests;

[Collection("AxisSagaPostgresCollection")]
public class AxisSagaPostgresDependencyInjectionTests(AxisSagaPostgresFixture fixture)
{
    [Fact]
    public void AddAxisSagaPostgresThrowsOnDoubleRegistration()
    {
        var services = new ServiceCollection();
        services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString });

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = fixture.ConnectionString }));
        Assert.Contains("already been registered", ex.Message);
    }
}
