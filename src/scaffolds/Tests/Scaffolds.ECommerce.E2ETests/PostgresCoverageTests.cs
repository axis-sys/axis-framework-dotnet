using Scaffolds.ECommerce.E2ETests.Fixtures;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.EmailValidation;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.GoogleLogin;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.MsEntraLogin;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

namespace Scaffolds.ECommerce.E2ETests;

// The single [Fact] holder for the Postgres-pinned run: every use-case file under UseCases/ exposes plain
// static tests, and this class fans them out under the Postgres collection (one container, one factory for
// the whole run; xUnit serializes facts within the class). Mirrors MySqlCoverageTests exactly, minus the
// [Fact(Skip)] guard, which flips with ECommerceProviderPin.DefaultProvider (testing-e2e-hermetic-provider-pinning).
[Collection(PostgresECommerceWebAppCollection.Name)]
public sealed class PostgresCoverageTests(PostgresECommerceWebAppFactory factory)
{
    private const string? Skip = ECommerceProviderPin.DefaultProvider == "Postgres"
        ? null
        : "Pinned provider is " + ECommerceProviderPin.DefaultProvider + " (see ECommerceProviderPin.DefaultProvider).";

    #region Composition

    [Fact(Skip = Skip)]
    public void AllControllersActivate() => CompositionTests.AllControllersActivateFromProductionContainer(factory);

    #endregion

    #region Auth

    [Fact(Skip = Skip)]
    public Task MsEntraLoginIssuesToken() => MsEntraLoginTests.ExchangesEntraTokenForBootstrapTokenAsync(factory);

    [Fact(Skip = Skip)]
    public Task MsEntraLoginAnonymous401() => MsEntraLoginTests.AnonymousReturns401Async(factory);

    [Fact(Skip = Skip)]
    public Task MsEntraLoginNoSubject401() => MsEntraLoginTests.TokenWithoutUsableSubjectReturns401Async(factory);

    [Fact(Skip = Skip)]
    public Task GoogleLoginIssuesToken() => GoogleLoginTests.ExchangesGoogleTokenForBootstrapTokenAsync(factory);

    [Fact(Skip = Skip)]
    public Task GoogleLoginAnonymous401() => GoogleLoginTests.AnonymousReturns401Async(factory);

    [Fact(Skip = Skip)]
    public Task EmailValidationRoundTrip() => EmailValidationTests.RoundTripValidatesTheEmailAsync(factory);

    [Fact(Skip = Skip)]
    public Task EmailValidationMismatch400() => EmailValidationTests.MismatchedCodeReturns400Async(factory);

    [Fact(Skip = Skip)]
    public Task EmailValidationAnonymous401() => EmailValidationTests.AnonymousRequestReturns401Async(factory);

    #endregion

    #region Catalog

    [Fact(Skip = Skip)]
    public Task CatalogRegisterAndRead() => CatalogTests.AdminRegistersThenReadsBackTheProductAsync(factory);

    [Fact(Skip = Skip)]
    public Task CatalogCheckoutThenSubmitOrder() => CatalogTests.CustomerChecksOutThenSubmitsTheOrderForTheSameCartAsync(factory);

    [Fact(Skip = Skip)]
    public Task CatalogAnonymousWrite401() => CatalogTests.AnonymousWriteReturns401Async(factory);

    [Fact(Skip = Skip)]
    public Task CatalogAnonymousCheckout401() => CatalogTests.AnonymousCheckoutReturns401Async(factory);

    [Fact(Skip = Skip)]
    public Task CatalogNoPermission403() => CatalogTests.AuthenticatedWithoutPermissionReturns403Async(factory);

    [Fact(Skip = Skip)]
    public Task CatalogSubmitOrderWithoutCheckout404() => CatalogTests.SubmitOrderWithoutPriorCheckoutReturns404Async(factory);

    #endregion
}
