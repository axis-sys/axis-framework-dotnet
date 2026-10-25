using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class GetProductHandlerTests : CatalogTestHost
{
    [Fact]
    public async Task MapsProductToResponseWhenFoundAsync()
    {
        var product = new FakeProduct(ProductId.New, (Sku)"SKU-1", "Keyboard", Stock: 5);
        ProductsReader.Setup(p => p.GetByIdAsync(product.ProductId))
            .ReturnsAsync(AxisResult.Ok<IProductEntityProperties>(product));
        var facade = Build<IProductsFacade>();

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = product.ProductId });

        var response = result.ShouldSucceed();
        Assert.Equal("Keyboard", response.Name);
        Assert.Equal(5, response.Stock);
    }

    [Fact]
    public async Task FailsWithNotFoundWhenMissingAsync()
    {
        var missingId = ProductId.New;
        ProductsReader.Setup(p => p.GetByIdAsync(missingId))
            .ReturnsAsync(ProductErrors.ProductNotFound);
        var facade = Build<IProductsFacade>();

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = missingId });

        result.ShouldFailWithCode(ProductErrors.ProductNotFound.Code);
        Assert.Equal(AxisErrorType.NotFound, result.Errors[0].Type);
    }

    [Fact]
    public async Task FailsValidationWhenTheIdIsNotAVersion7GuidAsync()
    {
        var facade = Build<IProductsFacade>();

        var result = await facade.GetProductAsync(new GetProductQuery { ProductId = Guid.NewGuid().ToString() });

        result.ShouldFailWithCode(ProductErrors.ProductIdInvalid);
    }
}
