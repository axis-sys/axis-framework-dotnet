namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresECommerceWebAppCollection : ICollectionFixture<PostgresECommerceWebAppFactory>
{
    public const string Name = "PostgresECommerceWebApp";
}
