namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class ProductsRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackEveryColumnWhenAProductIsCreatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Keyboard", Stock: 5);
        await CreateAsync(services, product);

        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();
        var got = (await products.GetByIdAsync(product.ProductId)).ShouldSucceed();

        Assert.Equal("Keyboard", got.Name);
        Assert.Equal(5, got.Stock);
        Assert.Equal(product.Sku, got.Sku);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FindsTheProductWhenReadBySkuAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Mouse", Stock: 1);
        await CreateAsync(services, product);

        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();
        var got = (await products.GetBySkuAsync(product.Sku)).ShouldSucceed();

        Assert.Equal(product.ProductId, got.ProductId);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenSkuAlreadyExistsAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var sku = TestData.NewSku();
        await CreateAsync(services, new FakeProduct(ProductId.New, sku, "First", 1));

        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        AxisResult duplicate = await products.CreateAsync(new FakeProduct(ProductId.New, sku, "Second", 1));

        duplicate.ShouldFailWithCode(ProductErrors.SkuAlreadyExists.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheSkusDifferOnlyByCaseAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var typed = $"sku-{Guid.NewGuid():N}";
        await CreateAsync(services, new FakeProduct(ProductId.New, (Sku)typed, "Lower", 1));

        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        AxisResult duplicate = await products.CreateAsync(new FakeProduct(ProductId.New, (Sku)typed.ToUpperInvariant(), "Upper", 1));

        duplicate.ShouldFailWithCode(ProductErrors.SkuAlreadyExists.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task StoresTheSkuInUpperCaseWhenItWasTypedInLowerCaseAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var typed = $"sku-{Guid.NewGuid():N}";
        var product = new FakeProduct(ProductId.New, (Sku)typed, "Lower", 1);
        await CreateAsync(services, product);

        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();
        var got = (await products.GetBySkuAsync((Sku)typed.ToUpperInvariant())).ShouldSucceed();

        Assert.Equal(typed.ToUpperInvariant(), got.Sku.ToString());
        Assert.Equal(product.ProductId, got.ProductId);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task DecrementsTheStockWhenStockIsReservedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Monitor", Stock: 5);
        await CreateAsync(services, product);

        using (var scope = services.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await products.ReserveStockAsync(product.ProductId, 2)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var products = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();
            Assert.Equal(3, (await products.GetByIdAsync(product.ProductId)).ShouldSucceed().Stock);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsTheProductWhenLockedForUpdateInsideARenewedTransactionAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var product = new FakeProduct(ProductId.New, TestData.NewSku(), "Speaker", Stock: 7);
        await CreateAsync(services, product);
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await unitOfWork.RenewAsync()).ShouldSucceed();
        var locked = await products.GetByIdForUpdateAsync(product.ProductId);

        Assert.Equal(7, locked.ShouldSucceed().Stock);
        (await products.GetByIdForUpdateAsync(ProductId.New)).ShouldFailWithCode(ProductErrors.ProductNotFound.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task SharesOneRepositoryInstanceWhenBothPortsResolveInTheSameScopeAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();

        object reader = scope.ServiceProvider.GetRequiredService<IProductsReaderPort>();
        object writer = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();

        Assert.Same(reader, writer);
    }

    private static async Task CreateAsync(ServiceProvider services, FakeProduct product)
    {
        using var scope = services.CreateScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductsWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await products.CreateAsync(product)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }
}
