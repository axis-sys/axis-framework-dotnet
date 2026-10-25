using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class EnsureCustomerConcurrencyDualDbTests(DualDbFixture fixture)
{
    private const int ConcurrentFirstSights = 8;

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ProvisionsOneCustomerWhenConcurrentFirstSightsReadThenRaceForTheEmailAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var email = TestData.NewEmail();

        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentFirstSights).Select(_ => EnsureAsync(services, email)));

        var customerIds = results.Select(result => result.ShouldSucceed().CustomerId).Distinct().ToList();
        Assert.Single(customerIds);
        Assert.Equal(customerIds[0], (await ReadByEmailAsync(services, email)).CustomerId.ToString());
    }

    private static async Task<AxisResult<EnsureCustomerResponse>> EnsureAsync(ServiceProvider services, string email)
    {
        await Task.Yield();
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersFacade>();

        return await customers.EnsureCustomerAsync(new EnsureCustomerCommand
        {
            ExternalId = $"external-{email}",
            Email = email,
            Name = "Racing Customer",
            Provider = "MsEntra",
        });
    }

    private static async Task<ICustomerEntityProperties> ReadByEmailAsync(ServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();

        return (await customers.GetByEmailAsync(email)).ShouldSucceed();
    }
}
