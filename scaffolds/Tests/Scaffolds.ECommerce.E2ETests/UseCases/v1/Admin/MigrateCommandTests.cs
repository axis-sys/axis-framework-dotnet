using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;
using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.Host.Persistence;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Admin;

public static class MigrateCommandTests
{
    private static readonly string[] Schemas = [CustomersDbInit.Schema, CatalogDbInit.Schema, EmailValidationDbInit.Schema, AuthorizationDbInit.Schema];

    public static async Task MigratesEverySchemaWhenTheMigrateVerbRunsOnAnEmptyDatabaseAsync(ECommerceWebAppFactory factory)
    {
        await using var server = await factory.StartEmptyServerAsync();

        var run = await factory.RunHostCommandAsync(server.ConnectionString, MigrateCommand.Verb);

        Assert.True(run.ExitCode == MigrateCommand.Succeeded, run.Output);
        foreach (var schema in Schemas)
            Assert.True(await server.CountTablesAsync(schema) > 0, $"The migrate verb created no table in {schema}.");
        var rerun = await factory.RunHostCommandAsync(server.ConnectionString, MigrateCommand.Verb);
        Assert.True(rerun.ExitCode == MigrateCommand.Succeeded, rerun.Output);
    }

    public static async Task ExitsWithTheFailureCodeWhenTheMigrationFailsAsync(ECommerceWebAppFactory factory)
    {
        var refused = factory.ConnectionStringWithPassword("not-the-password");

        var run = await factory.RunHostCommandAsync(refused, MigrateCommand.Verb);

        Assert.True(run.ExitCode == MigrateCommand.Failed, run.Output);
    }
}
