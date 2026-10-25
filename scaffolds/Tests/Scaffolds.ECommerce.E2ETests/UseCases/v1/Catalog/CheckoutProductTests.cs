using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

public static class CheckoutProductTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ReservesTheStockWhenTheCustomerChecksOutAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct(initialStock: 10));
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var checkout = new CheckoutProductCommand { CartId = Guid.CreateVersion7().ToString(), Quantity = 2 };

        var response = await customer.PostAsJsonAsync(Routes.ProductCheckout(productId), checkout, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var checkedOut = await response.Content.ReadFromJsonAsync<CheckoutProductResponse>(Ct);
        Assert.NotNull(checkedOut);
        Assert.Equal(productId, checkedOut.ProductId);
        var product = await customer.GetFromJsonAsync<GetProductResponse>(Routes.Product(productId), Ct);
        Assert.NotNull(product);
        Assert.Equal(8, product.Stock);
    }

    public static async Task ReturnsUnprocessableEntityWhenTheQuantityExceedsTheStockAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct(initialStock: 1));
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var checkout = new CheckoutProductCommand { CartId = Guid.CreateVersion7().ToString(), Quantity = 2 };

        var response = await customer.PostAsJsonAsync(Routes.ProductCheckout(productId), checkout, Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        var checkout = new CheckoutProductCommand { CartId = Guid.CreateVersion7().ToString(), Quantity = 1 };

        var response = await client.PostAsJsonAsync(Routes.ProductCheckout(Guid.CreateVersion7().ToString()), checkout, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
