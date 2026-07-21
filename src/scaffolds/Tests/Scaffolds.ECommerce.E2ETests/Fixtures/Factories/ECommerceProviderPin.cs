namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

// Compile-time provider pin (testing-e2e-hermetic-provider-pinning): flip this constant to run the E2E
// suite against the other dialect. Each provider's coverage class derives its [Fact(Skip)] guard from
// this constant, so the non-pinned provider's whole collection skips visibly rather than silently.
internal static class ECommerceProviderPin
{
    public const string DefaultProvider = "Postgres";
}
