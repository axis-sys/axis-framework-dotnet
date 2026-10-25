using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.RegisterProduct;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

internal static class CatalogSteps
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static RegisterProductCommand NewProduct(string name = "Fixture Product", int initialStock = 10)
        => new() { Sku = TestData.NewSku(), Name = name, InitialStock = initialStock };

    public static async Task<string> RegisterProductAsync(HttpClient registrar, RegisterProductCommand product)
    {
        var response = await registrar.PostAsJsonAsync(Routes.Products, product, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RegisterProductResponse>(Ct);
        Assert.NotNull(created);
        return created.ProductId;
    }

    public static async Task<SubmitOrderResponse> SubmitOrderOnceTheCartIsAssociatedAsync(
        HttpClient client, string cartId, int quantity, int maxAttempts = 100, int delayMilliseconds = 100)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await client.PostAsJsonAsync(Routes.Orders, new SubmitOrderCommand { Quantity = quantity, CartId = cartId }, Ct);
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
}
