namespace Scaffolds.ECommerce.IntegrationTests;

[CollectionDefinition(Name)]
public sealed class DualDbCollection : ICollectionFixture<DualDbFixture>
{
    public const string Name = "DualDbECommerceCollection";
}
