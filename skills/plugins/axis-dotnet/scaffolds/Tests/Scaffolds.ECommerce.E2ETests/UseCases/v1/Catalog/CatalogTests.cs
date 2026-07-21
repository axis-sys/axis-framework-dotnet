using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1._Shared;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;
using Scaffolds.ECommerce.E2ETests.Fixtures;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

/// <summary>The catalog journey plus its 401/403/404 edges (testing-e2e-controller-coverage-gate).</summary>
public sealed class CatalogTests
{
    private const string ProductsEndpoint = "/api/v1/catalog/products";
    private const string SubmitOrderEndpoint = "/api/v1/catalog/orders/submit";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Happy path tests
    public static async Task AdminRegistersThenReadsBackTheProductAsync(ECommerceWebAppFactory factory)
    {
        var client = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var command = new RegisterProductCommand { Sku = TestData.NewSku(), Name = "Keyboard", InitialStock = 5 };

        var register = await client.PostAsJsonAsync(ProductsEndpoint, command, Ct);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var created = await register.Content.ReadFromJsonAsync<RegisterProductResponse>(Ct);
        Assert.NotNull(created);

        var read = await client.GetAsync($"/api/v1/catalog/{created.ProductId}", Ct);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var product = await read.Content.ReadFromJsonAsync<GetProductResponse>(Ct);
        Assert.NotNull(product);
        Assert.Equal("Keyboard", product.Name);
        Assert.Equal(5, product.Stock);
    }

    /// <summary>
    /// Checkout publishes ProductCheckedOutEvent (architecture-bus-events); the cart association it drives
    /// lands out of band through the atomic outbox, so submit is polled until the consumer has caught up.
    /// </summary>
    public static async Task CustomerChecksOutThenSubmitsTheOrderForTheSameCartAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var productId = await RegisterAsync(admin, initialStock: 10);
        var client = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var cartId = Guid.CreateVersion7().ToString();

        var checkout = await client.PostAsJsonAsync($"/api/v1/catalog/{productId}/checkout", new CheckoutCommand { CartId = cartId, Quantity = 2 }, Ct);
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkedOut = await checkout.Content.ReadFromJsonAsync<CheckoutResponse>(Ct);
        Assert.NotNull(checkedOut);
        Assert.Equal(productId, checkedOut.ProductId);

        var submitted = await SubmitUntilCartIsAssociatedAsync(client, cartId, quantity: 2);
        Assert.Equal(productId, submitted.ProductId);
        Assert.Equal(2, submitted.Quantity);
    }

    private static async Task<string> RegisterAsync(HttpClient admin, int initialStock)
    {
        var command = new RegisterProductCommand { Sku = TestData.NewSku(), Name = "Fixture Product", InitialStock = initialStock };
        var response = await admin.PostAsJsonAsync(ProductsEndpoint, command, Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RegisterProductResponse>(Ct);
        Assert.NotNull(created);
        return created.ProductId;
    }

    // The checkout event's atomic-outbox delivery is asynchronous (AxisBusRepositorySettings.PollInterval);
    // 404 (CART_ITEM_NOT_FOUND) just means the consumer has not caught up yet, so retry submit until it has.
    private static async Task<SubmitOrderResponse> SubmitUntilCartIsAssociatedAsync(
        HttpClient client, string cartId, int quantity, int maxAttempts = 100, int delayMilliseconds = 100)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await client.PostAsJsonAsync(SubmitOrderEndpoint, new SubmitOrderCommand { Quantity = quantity, CartId = cartId }, Ct);
            if (response.StatusCode == HttpStatusCode.Created)
            {
                var body = await response.Content.ReadFromJsonAsync<SubmitOrderResponse>(Ct);
                Assert.NotNull(body);
                return body;
            }

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            await Task.Delay(delayMilliseconds, Ct);
        }

        throw new TimeoutException($"Cart '{cartId}' was never associated with a product by the checkout consumer.");
    }

    // Negative path tests
    public static async Task AnonymousWriteReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        var command = new RegisterProductCommand { Sku = TestData.NewSku(), Name = "Mouse", InitialStock = 1 };

        var response = await client.PostAsJsonAsync(ProductsEndpoint, command, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task AnonymousCheckoutReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/api/v1/catalog/{Guid.NewGuid()}/checkout", new { quantity = 1 }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task AuthenticatedWithoutPermissionReturns403Async(ECommerceWebAppFactory factory)
    {
        var client = await factory.ECommerceClientAsync(EUserType.NormalUser);
        var command = new RegisterProductCommand { Sku = TestData.NewSku(), Name = "Mouse", InitialStock = 1 };

        var response = await client.PostAsJsonAsync(ProductsEndpoint, command, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public static async Task SubmitOrderWithoutPriorCheckoutReturns404Async(ECommerceWebAppFactory factory)
    {
        var client = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await client.PostAsJsonAsync(SubmitOrderEndpoint, new SubmitOrderCommand { Quantity = 1, CartId = Guid.CreateVersion7().ToString() }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
