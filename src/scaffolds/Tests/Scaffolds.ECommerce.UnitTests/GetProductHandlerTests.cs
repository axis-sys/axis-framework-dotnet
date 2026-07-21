using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;
using Sku = Scaffolds.ECommerce.SharedKernel.Catalog.Sku;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class GetProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task MapsProductToResponseWhenFoundAsync()
    {
        var product = new FakeProduct(ProductId.New, (Sku)"SKU-1", "Keyboard", Stock: 5);
        Products.Setup(p => p.GetByIdAsync(product.ProductId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(product));
        var facade = Build();

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = product.ProductId });

        var response = result.ShouldSucceed();
        Assert.Equal("Keyboard", response.Name);
        Assert.Equal(5, response.Stock);
    }

    [Fact]
    public async Task FailsWithNotFoundWhenMissingAsync()
    {
        var missingId = ProductId.New;
        Products.Setup(p => p.GetByIdAsync(missingId))
            .ReturnsAsync(AxisError.NotFound("PRODUCT_NOT_FOUND"));
        var facade = Build();

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = missingId });

        Assert.True(result.IsFailure);
        Assert.Equal(AxisErrorType.NotFound, result.Errors[0].Type);
    }
}
