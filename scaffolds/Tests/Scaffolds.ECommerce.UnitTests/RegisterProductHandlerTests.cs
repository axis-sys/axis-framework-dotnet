using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class RegisterProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task RegistersNewProductWhenSkuIsFreeAsync()
    {
        var facade = Build<IProductsFacade>();

        var result = await facade.RegisterProductAsync(new RegisterProductCommand { Sku = "SKU-NEW", Name = "Mouse", InitialStock = 10 });

        Assert.True(result.IsSuccess);
        ProductsWrite.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
    }

    [Fact]
    public async Task RecoversExistingProductWhenSkuAlreadyExistsAsync()
    {
        var existing = new FakeProduct(ProductId.New, (Sku)"SKU-DUP", "Mouse", 1);
        ProductsWrite.Setup(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()))
            .ReturnsAsync(ProductErrors.SkuAlreadyExists);
        ProductsReader.Setup(p => p.GetBySkuAsync((Sku)"SKU-DUP"))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(existing));
        var facade = Build<IProductsFacade>();

        var result = await facade.RegisterProductAsync(new RegisterProductCommand { Sku = "SKU-DUP", Name = "Mouse", InitialStock = 10 });

        var response = result.ShouldSucceed();
        Assert.Equal(existing.ProductId, response.ProductId);
        ProductsWrite.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
        ProductsReader.Verify(p => p.GetBySkuAsync((Sku)"SKU-DUP"), Times.Once);
    }
}
