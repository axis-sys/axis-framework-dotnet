namespace Scaffolds.ECommerce.E2ETests.Fixtures;

// Collision-free identifiers so every test can reuse the one shared in-memory store safely.
public static class TestData
{
    public static string NewSku(string prefix = "SKU") => $"{prefix}-{Guid.NewGuid():N}";
}
