using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class CheckoutConcurrencyDualDbTests(DualDbFixture fixture)
{
    private const int InitialStock = 5;
    private const int ConcurrentCheckouts = 12;

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task SellsExactlyTheStockWhenConcurrentCheckoutsRaceThroughTheProductionHandlerAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Contended", InitialStock);
        await SeedAsync(services, product);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = AxisEntityId.New;

        var results = await Task.WhenAll(
            Enumerable.Range(0, ConcurrentCheckouts).Select(_ => CheckoutOneUnitAsync(services, product.ProductId)));

        Assert.Equal(InitialStock, results.Count(result => result.IsSuccess));
        Assert.All(results.Where(result => result.IsFailure), result => result.ShouldFailWithCode(ProductErrors.InsufficientStock.Code));
        Assert.Equal(0, await ReadStockAsync(services, product.ProductId));
    }

    private static async Task SeedAsync(ServiceProvider services, FakeProduct product)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await products.CreateAsync(product)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<AxisResult<CheckoutProductResponse>> CheckoutOneUnitAsync(ServiceProvider services, ProductId productId)
    {
        await Task.Yield();
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsFacade>();

        return await products.CheckoutProductAsync(new CheckoutProductCommand { CartId = CartId.New, ProductId = productId, Quantity = 1 });
    }

    private static async Task<int> ReadStockAsync(ServiceProvider services, ProductId productId)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();

        return (await products.GetByIdAsync(productId)).ShouldSucceed().Stock;
    }
}
