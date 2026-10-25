using Scaffolds.ECommerce.E2ETests.Documentation;
using Scaffolds.ECommerce.E2ETests.Fixtures;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Admin;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Authorization;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;
using Scaffolds.ECommerce.E2ETests.UseCases.v1.EmailValidation;

namespace Scaffolds.ECommerce.E2ETests;

[Collection(PostgresECommerceWebAppCollection.Name)]
public sealed class PostgresCoverageTests(PostgresECommerceWebAppFactory factory)
{
    private const string? Skip = ECommerceProviderPin.DefaultProvider == "Postgres"
        ? null
        : "Pinned provider is " + ECommerceProviderPin.DefaultProvider + " (see ECommerceProviderPin.DefaultProvider).";

    [Fact(Skip = Skip)]
    public void ActivatesEveryControllerWhenResolvedFromTheProductionContainer()
        => CompositionTests.ActivatesEveryControllerWhenResolvedFromTheProductionContainer(factory);

    [Fact(Skip = Skip)]
    public Task DocumentsEveryOperationWithItsResponsesWhenTheHostRunsInDevelopmentAsync()
        => OpenApiDocumentTests.DocumentsEveryOperationWithItsResponsesWhenTheHostRunsInDevelopmentAsync(factory);

    [Fact(Skip = Skip)]
    public Task DescribesTheApiAndItsSchemesWhenTheHostRunsInDevelopmentAsync()
        => OpenApiDocumentTests.DescribesTheApiAndItsSchemesWhenTheHostRunsInDevelopmentAsync(factory);

    [Fact(Skip = Skip)]
    public Task IssuesASessionTokenWhenTheMsEntraTokenIsValidAsync()
        => RequestTokenTests.IssuesASessionTokenWhenTheMsEntraTokenIsValidAsync(factory);

    [Fact(Skip = Skip)]
    public Task IssuesASessionTokenWhenTheGoogleTokenIsValidAsync()
        => RequestTokenTests.IssuesASessionTokenWhenTheGoogleTokenIsValidAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheSessionWhenTheCallerIsAnonymousAsync()
        => RequestTokenTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheSessionWhenTheExternalTokenHasNoUsableSubjectAsync()
        => RequestTokenTests.ReturnsUnauthorizedWhenTheExternalTokenHasNoUsableSubjectAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheSessionWhenTheCallerPresentsASessionTokenAsync()
        => RequestTokenTests.ReturnsUnauthorizedWhenTheCallerPresentsASessionTokenAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReadsTheProductWhenItIsInTheCatalogAsync()
        => GetProductTests.ReadsTheProductWhenItIsInTheCatalogAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundWhenTheProductIsNotInTheCatalogAsync()
        => GetProductTests.ReturnsNotFoundWhenTheProductIsNotInTheCatalogAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheProductReadWhenTheCallerIsAnonymousAsync()
        => GetProductTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task RegistersTheProductWhenTheMemberHoldsThePermissionAsync()
        => RegisterProductTests.RegistersTheProductWhenTheMemberHoldsThePermissionAsync(factory);

    [Fact(Skip = Skip)]
    public Task RegistersTheProductWhenTheCallerIsAnAdministratorAsync()
        => RegisterProductTests.RegistersTheProductWhenTheCallerIsAnAdministratorAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsForbiddenOnTheRegistrationWhenTheCallerLacksThePermissionAsync()
        => RegisterProductTests.ReturnsForbiddenWhenTheCallerLacksThePermissionAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsForbiddenOnTheRegistrationWhenTheMemberIsDisabledAsync()
        => RegisterProductTests.ReturnsForbiddenWhenTheMemberIsDisabledAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheRegistrationWhenTheCallerIsAnonymousAsync()
        => RegisterProductTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReservesTheStockWhenTheCustomerChecksOutAsync()
        => CheckoutProductTests.ReservesTheStockWhenTheCustomerChecksOutAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnprocessableEntityWhenTheQuantityExceedsTheStockAsync()
        => CheckoutProductTests.ReturnsUnprocessableEntityWhenTheQuantityExceedsTheStockAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheCheckoutWhenTheCallerIsAnonymousAsync()
        => CheckoutProductTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task SubmitsTheOrderWhenTheCartWasCheckedOutAsync()
        => SubmitOrderTests.SubmitsTheOrderWhenTheCartWasCheckedOutAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundWhenTheCartWasNeverCheckedOutAsync()
        => SubmitOrderTests.ReturnsNotFoundWhenTheCartWasNeverCheckedOutAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheOrderWhenTheCallerIsAnonymousAsync()
        => SubmitOrderTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReadsThePricedOrderWhenTheCustomerOwnsItAsync()
        => GetOrderTests.ReadsThePricedOrderWhenTheCustomerOwnsItAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundWhenTheOrderBelongsToAnotherCustomerAsync()
        => GetOrderTests.ReturnsNotFoundWhenTheOrderBelongsToAnotherCustomerAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundWhenTheOrderIsUnknownAsync()
        => GetOrderTests.ReturnsNotFoundWhenTheOrderIsUnknownAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheOrderReadWhenTheCallerIsAnonymousAsync()
        => GetOrderTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task EmailsACodeToTheAddressOnRecordWhenTheCustomerAsksAsync()
        => RequestEmailValidationTests.EmailsACodeToTheAddressOnRecordWhenTheCustomerAsksAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheCodeRequestWhenTheCallerIsAnonymousAsync()
        => RequestEmailValidationTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ValidatesTheEmailWhenTheCustomerEchoesTheEmailedCodeAsync()
        => ValidateEmailTests.ValidatesTheEmailWhenTheCustomerEchoesTheEmailedCodeAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsBadRequestWhenTheCodeDoesNotMatchAsync()
        => ValidateEmailTests.ReturnsBadRequestWhenTheCodeDoesNotMatchAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheValidationWhenTheCallerIsAnonymousAsync()
        => ValidateEmailTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReadsTheRunWhenTheCallerIsAnAdministratorAsync()
        => GetValidationStatusTests.ReadsTheRunWhenTheCallerIsAnAdministratorAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsTheUnknownRunAnswerWhenTheCallerLacksTheSagaPermissionAsync()
        => GetValidationStatusTests.ReturnsTheUnknownRunAnswerWhenTheCallerLacksTheSagaPermissionAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundWhenTheRunIsUnknownAsync()
        => GetValidationStatusTests.ReturnsNotFoundWhenTheRunIsUnknownAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheRunWhenTheCallerIsAnonymousAsync()
        => GetValidationStatusTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task CancelsTheRunGracefullyWhenItIsStillRunningAsync()
        => CancelValidationTests.CancelsTheRunGracefullyWhenItIsStillRunningAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsConflictOnCancelWhenTheRunAlreadyCompletedAsync()
        => CancelValidationTests.ReturnsConflictWhenTheRunAlreadyCompletedAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsTheUnknownRunAnswerOnCancelWhenTheCallerLacksTheSagaPermissionAsync()
        => CancelValidationTests.ReturnsTheUnknownRunAnswerWhenTheCallerLacksTheSagaPermissionAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnCancelWhenTheCallerIsAnonymousAsync()
        => CancelValidationTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task MigratesEverySchemaInDependencyOrderWhenAnAdminRepeatsTheMigrationAsync()
        => MigrateDatabaseTests.MigratesEverySchemaInDependencyOrderWhenAnAdminRepeatsTheMigrationAsync(factory);

    [Fact(Skip = Skip)]
    public Task MigratesEverySchemaWhenTheMigrateVerbRunsOnAnEmptyDatabaseAsync()
        => MigrateCommandTests.MigratesEverySchemaWhenTheMigrateVerbRunsOnAnEmptyDatabaseAsync(factory);

    [Fact(Skip = Skip)]
    public Task ExitsWithTheFailureCodeWhenTheMigrateVerbCannotMigrateAsync()
        => MigrateCommandTests.ExitsWithTheFailureCodeWhenTheMigrationFailsAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheMigrationWhenTheCallerIsAnonymousAsync()
        => MigrateDatabaseTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsForbiddenOnTheMigrationWhenTheCallerIsNotAnAdminAsync()
        => MigrateDatabaseTests.ReturnsForbiddenWhenTheCallerIsNotAnAdminAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReplacesTheGrantsWhenAnAdministratorUpdatesThemAsync()
        => UpdateGrantsTests.ReplacesTheGrantsWhenAnAdministratorUpdatesThemAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsForbiddenOnTheGrantsWhenTheCallerIsNotAnAdministratorAsync()
        => UpdateGrantsTests.ReturnsForbiddenWhenTheCallerIsNotAnAdministratorAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsUnauthorizedOnTheGrantsWhenTheCallerIsAnonymousAsync()
        => UpdateGrantsTests.ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsBadRequestOnTheGrantsWhenAPermissionIsUnknownAsync()
        => UpdateGrantsTests.ReturnsBadRequestWhenAPermissionIsUnknownAsync(factory);

    [Fact(Skip = Skip)]
    public Task ReturnsNotFoundOnTheGrantsWhenTheCustomerIsUnknownAsync()
        => UpdateGrantsTests.ReturnsNotFoundWhenTheCustomerIsUnknownAsync(factory);

    [Fact(Skip = Skip)]
    public Task AuthorizesTheNextRequestsOfTheMemberWhenAnAdministratorGrantsThePermissionAsync()
        => UpdateGrantsTests.AuthorizesTheNextRequestsOfTheMemberWhenAnAdministratorGrantsThePermissionAsync(factory);

    [Fact(Skip = Skip)]
    public Task RefusesTheNextRequestsOfTheMemberWhenAnAdministratorDisablesTheMembershipAsync()
        => UpdateGrantsTests.RefusesTheNextRequestsOfTheMemberWhenAnAdministratorDisablesTheMembershipAsync(factory);
}
