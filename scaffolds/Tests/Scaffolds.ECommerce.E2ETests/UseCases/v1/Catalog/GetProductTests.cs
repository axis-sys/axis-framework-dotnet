using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

public static class GetProductTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ReadsTheProductWhenItIsInTheCatalogAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct("Keyboard", initialStock: 5));
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.GetAsync(Routes.Product(productId), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var product = await response.Content.ReadFromJsonAsync<GetProductResponse>(Ct);
        Assert.NotNull(product);
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(5, product.Stock);
    }

    public static async Task ReturnsNotFoundWhenTheProductIsNotInTheCatalogAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.GetAsync(Routes.Product(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.Product(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
