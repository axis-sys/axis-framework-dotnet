using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

public static class GetOrderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ReadsThePricedOrderWhenTheCustomerOwnsItAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var submitted = await SubmitAnOrderAsync(factory, customer, quantity: 2);

        var response = await customer.GetAsync(Routes.Order(submitted.OrderId), Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
        var order = await response.Content.ReadFromJsonAsync<GetOrderResponse>(Ct);
        Assert.NotNull(order);
        Assert.Equal(submitted.OrderId, order.OrderId);
        Assert.Equal(submitted.ProductId, order.ProductId);
        Assert.Equal(2, order.Quantity);
        Assert.StartsWith($"Order {submitted.OrderId} totals ", order.Summary);
        Assert.StartsWith($"Order {submitted.OrderId}: 2 x product {submitted.ProductId}, total ", order.Description);
    }

    public static async Task ReturnsNotFoundWhenTheOrderBelongsToAnotherCustomerAsync(ECommerceWebAppFactory factory)
    {
        var (owner, _, _) = await factory.NewCustomerClientAsync();
        var submitted = await SubmitAnOrderAsync(factory, owner, quantity: 1);
        var (stranger, _, _) = await factory.NewCustomerClientAsync();

        var response = await stranger.GetAsync(Routes.Order(submitted.OrderId), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([OrderErrors.OrderNotFound.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsNotFoundWhenTheOrderIsUnknownAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.GetAsync(Routes.Order(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.Order(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<SubmitOrderResponse> SubmitAnOrderAsync(ECommerceWebAppFactory factory, HttpClient customer, int quantity)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct(initialStock: 10));
        var cartId = Guid.CreateVersion7().ToString();
        var checkout = await customer.PostAsJsonAsync(Routes.ProductCheckout(productId), new CheckoutProductCommand { CartId = cartId, Quantity = quantity }, Ct);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        return await CatalogSteps.SubmitOrderOnceTheCartIsAssociatedAsync(customer, cartId, quantity);
    }
}
