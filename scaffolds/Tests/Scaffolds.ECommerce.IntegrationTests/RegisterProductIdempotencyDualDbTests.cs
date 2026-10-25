using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class RegisterProductIdempotencyDualDbTests(DualDbFixture fixture)
{
    private const int ConcurrentRegistrations = 8;

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReturnsTheRegisteredProductWhenTheSkuIsRegisteredAgainAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var sku = TestData.NewSku();

        var registered = await RegisterAsync(services, sku, "Keyboard");
        var again = await RegisterAsync(services, sku, "Another keyboard");

        Assert.Equal(registered.ShouldSucceed().ProductId, again.ShouldSucceed().ProductId);
        Assert.Equal("Keyboard", (await ReadBySkuAsync(services, sku)).Name);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task RegistersOneProductWhenConcurrentRegistrationsRaceForTheSkuAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var sku = TestData.NewSku();

        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentRegistrations).Select(_ => RegisterAsync(services, sku, "Contended")));

        var productIds = results.Select(result => result.ShouldSucceed().ProductId).Distinct().ToList();
        Assert.Single(productIds);
        Assert.Equal(productIds[0], (await ReadBySkuAsync(services, sku)).ProductId.ToString());
    }

    private static async Task<AxisResult<RegisterProductResponse>> RegisterAsync(ServiceProvider services, Sku sku, string name)
    {
        await Task.Yield();
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsFacade>();

        return await products.RegisterProductAsync(new RegisterProductCommand { Sku = sku.ToString(), Name = name, InitialStock = 3 });
    }

    private static async Task<IProductEntityProperties> ReadBySkuAsync(ServiceProvider services, Sku sku)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();

        return (await products.GetBySkuAsync(sku)).ShouldSucceed();
    }
}
