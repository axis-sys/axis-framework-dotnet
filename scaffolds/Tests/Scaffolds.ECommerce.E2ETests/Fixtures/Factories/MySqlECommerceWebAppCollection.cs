namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlECommerceWebAppCollection : ICollectionFixture<MySqlECommerceWebAppFactory>
{
    public const string Name = "MySqlECommerceWebApp";
}
