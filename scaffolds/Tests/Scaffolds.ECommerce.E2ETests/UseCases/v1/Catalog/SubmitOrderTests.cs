using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

public static class SubmitOrderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task SubmitsTheOrderWhenTheCartWasCheckedOutAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct(initialStock: 10));
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var cartId = Guid.CreateVersion7().ToString();
        var checkout = await customer.PostAsJsonAsync(Routes.ProductCheckout(productId), new CheckoutProductCommand { CartId = cartId, Quantity = 2 }, Ct);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var submitted = await CatalogSteps.SubmitOrderOnceTheCartIsAssociatedAsync(customer, cartId, quantity: 2);

        Assert.Equal(productId, submitted.ProductId);
        Assert.Equal(2, submitted.Quantity);
        Assert.False(string.IsNullOrWhiteSpace(submitted.OrderId));
    }

    public static async Task ReturnsNotFoundWhenTheCartWasNeverCheckedOutAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var order = new SubmitOrderCommand { Quantity = 1, CartId = Guid.CreateVersion7().ToString() };

        var response = await customer.PostAsJsonAsync(Routes.Orders, order, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        var order = new SubmitOrderCommand { Quantity = 1, CartId = Guid.CreateVersion7().ToString() };

        var response = await client.PostAsJsonAsync(Routes.Orders, order, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
