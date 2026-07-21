using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class RegisterProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task RegistersNewProductWhenSkuIsFreeAsync()
    {
        var facade = Build();

        var result = await facade.RegisterProductAsync(new RegisterProductCommand { Sku = "SKU-NEW", Name = "Mouse", InitialStock = 10 });

        Assert.True(result.IsSuccess);
        Products.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
    }

    [Fact]
    public async Task RecoversExistingProductWhenSkuAlreadyExistsAsync()
    {
        var existing = new FakeProduct(ProductId.New, (Sku)"SKU-DUP", "Mouse", 1);
        // CreateAsync hits the unique-SKU constraint the repository maps to a Conflict...
        Products.Setup(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()))
            .ReturnsAsync(AxisError.Conflict("PRODUCT_SKU_ALREADY_EXISTS"));
        // ...and RecoverConflictAsync recovers by fetching the product that already exists.
        Products.Setup(p => p.GetBySkuAsync((Sku)"SKU-DUP"))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(existing));
        var facade = Build();

        var result = await facade.RegisterProductAsync(new RegisterProductCommand { Sku = "SKU-DUP", Name = "Mouse", InitialStock = 10 });

        var response = result.ShouldSucceed();
        Assert.Equal(existing.ProductId, response.ProductId);
        Products.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
        Products.Verify(p => p.GetBySkuAsync((Sku)"SKU-DUP"), Times.Once);
    }
}
