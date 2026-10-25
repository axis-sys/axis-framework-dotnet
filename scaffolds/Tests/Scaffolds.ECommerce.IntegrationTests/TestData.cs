namespace Scaffolds.ECommerce.IntegrationTests;

internal static class TestData
{
    public static Sku NewSku() => (Sku)$"SKU-{Guid.NewGuid():N}";

    public static string NewEmail() => $"customer-{Guid.NewGuid():N}@example.com";
}
