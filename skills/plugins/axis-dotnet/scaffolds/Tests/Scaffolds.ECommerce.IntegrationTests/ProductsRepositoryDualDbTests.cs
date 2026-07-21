using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection("DualDbECommerceCollection")]
public class ProductsRepositoryDualDbTests(DualDbFixture fixture)
{
    public enum Database { Postgres, MySql }

    private ServiceProvider BuildProvider(Database database)
    {
        var services = new ServiceCollection();
        services.AddAxisLogger();

        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(m => m.CancellationToken).Returns(CancellationToken.None);
        mediator.SetupGet(m => m.TraceId).Returns("trace");
        mediator.SetupGet(m => m.OriginId).Returns("origin");
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);

        if (database == Database.MySql)
            services.AddECommerceMySql(fixture.MySqlConnectionString);
        else
            services.AddECommercePostgres(fixture.PostgresConnectionString);

        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(Database.Postgres)]
    [InlineData(Database.MySql)]
    public async Task CreatesThenReadsBackProductByIdAsync(Database database)
    {
        await using var provider = BuildProvider(database);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Keyboard", Stock: 5);

        using (var scope = provider.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsPort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await products.CreateAsync(product)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = provider.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsPort>();
            var got = (await products.GetByIdAsync(product.ProductId)).ShouldSucceed();
            Assert.Equal("Keyboard", got.Name);
            Assert.Equal(5, got.Stock);
            Assert.Equal(product.Sku, got.Sku);
        }
    }

    [Theory]
    [InlineData(Database.Postgres)]
    [InlineData(Database.MySql)]
    public async Task FailsWithConflictWhenSkuAlreadyExistsAsync(Database database)
    {
        await using var provider = BuildProvider(database);
        var sku = TestData.NewSku();

        using (var scope = provider.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsPort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await products.CreateAsync(new FakeProduct(ProductId.New, sku, "First", 1))).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = provider.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsPort>();
            AxisResult duplicate = await products.CreateAsync(new FakeProduct(ProductId.New, sku, "Second", 1));
            Assert.True(duplicate.IsFailure);
            Assert.Contains(duplicate.Errors, e => e.Code == "PRODUCT_SKU_ALREADY_EXISTS");
        }
    }
}
