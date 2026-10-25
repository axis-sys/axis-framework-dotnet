using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class CreateProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task CreatesNewProductWhenSkuIsFreeAsync()
    {
        var facade = Build<IProductsFacade>();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-NEW", Name = "Mouse", InitialStock = 10 });

        Assert.True(result.IsSuccess);
        ProductsWrite.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
    }

    [Fact]
    public async Task CreatesTheProductWhenInitialStockIsZeroAsync()
    {
        var facade = Build<IProductsFacade>();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-ZERO", Name = "Mouse", InitialStock = 0 });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task FailsValidationWhenInitialStockIsMissingAsync()
    {
        var facade = Build<IProductsFacade>();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-NEW", Name = "Mouse" });

        result.ShouldFailWithCode(ProductErrors.InitialStockInvalid);
        ProductsWrite.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task FailsWithConflictWhenSkuAlreadyExistsAsync()
    {
        ProductsReader.Setup(p => p.GetBySkuAsync((Sku)"SKU-DUP"))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(new FakeProduct(ProductId.New, (Sku)"SKU-DUP", "Mouse", 1)));
        var facade = Build<IProductsFacade>();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-DUP", Name = "Mouse", InitialStock = 10 });

        result.ShouldFailWithCode(ProductErrors.SkuAlreadyExists.Code);
        ProductsWrite.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Never);
    }
}
