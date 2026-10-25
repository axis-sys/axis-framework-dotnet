using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;
using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

namespace Scaffolds.ECommerce.IntegrationTests;

public sealed class MigrateDatabaseDualDbTests
{
    private static readonly string[] DependencyOrder = [CustomersDbInit.Schema, CatalogDbInit.Schema, EmailValidationDbInit.Schema, AuthorizationDbInit.Schema];

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task MigratesEverySchemaInDependencyOrderWhenTheDatabaseIsEmptyAsync(DbProvider provider)
    {
        await using var server = await EmptyServer.StartAsync(provider);
        await using var services = DualDbServices.BuildAdministration(provider, server.Configuration);
        using var scope = services.CreateScope();
        var administration = scope.ServiceProvider.GetRequiredService<IAdminFacade>();

        var migrated = await administration.MigrateDatabaseAsync(new MigrateDatabaseCommand());

        Assert.Equal(DependencyOrder, migrated.ShouldSucceed().Schemas);
        foreach (var schema in DependencyOrder)
            Assert.True(await server.CountTablesAsync(schema) > 0, $"The migration created no table in {schema}.");
    }
}
