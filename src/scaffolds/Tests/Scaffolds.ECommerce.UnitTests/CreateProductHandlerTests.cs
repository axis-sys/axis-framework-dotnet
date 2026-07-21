using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class CreateProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task CreatesNewProductWhenSkuIsFreeAsync()
    {
        var facade = Build();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-NEW", Name = "Mouse", InitialStock = 10 });

        Assert.True(result.IsSuccess);
        Products.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Once);
    }

    [Fact]
    public async Task FailsWithConflictWhenSkuAlreadyExistsAsync()
    {
        // Guard variant: the SKU already exists, so RequireNotFound short-circuits to a conflict and
        // creation never runs (contrast RegisterProduct, which recovers the existing product on conflict).
        Products.Setup(p => p.GetBySkuAsync((Sku)"SKU-DUP"))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(new FakeProduct(ProductId.New, (Sku)"SKU-DUP", "Mouse", 1)));
        var facade = Build();

        var result = await facade.CreateProductAsync(new CreateProductCommand { Sku = "SKU-DUP", Name = "Mouse", InitialStock = 10 });

        Assert.True(result.IsFailure);
        Assert.Equal("PRODUCT_SKU_ALREADY_EXISTS", result.Errors[0].Code);
        Products.Verify(p => p.CreateAsync(It.IsAny<IProductEntityProperties>()), Times.Never);
    }
}
