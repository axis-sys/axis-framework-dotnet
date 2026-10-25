using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;
using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.Host.Authorization;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Admin;

public static class MigrateDatabaseTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task MigratesEverySchemaInDependencyOrderWhenAnAdminRepeatsTheMigrationAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        string[] dependencyOrder = [CustomersDbInit.Schema, CatalogDbInit.Schema, EmailValidationDbInit.Schema, AuthorizationDbInit.Schema];

        var response = await admin.PostAsync(Routes.DatabaseMigrations, content: null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var migrated = await response.Content.ReadFromJsonAsync<MigrateDatabaseResponse>(Ct);
        Assert.NotNull(migrated);
        Assert.Equal(dependencyOrder, migrated.Schemas);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Routes.DatabaseMigrations, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task ReturnsForbiddenWhenTheCallerIsNotAnAdminAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.PostAsync(Routes.DatabaseMigrations, content: null, Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([AuthorizationErrors.AdministratorRequired.Code], await ProblemResponses.ErrorCodesAsync(response));
    }
}
