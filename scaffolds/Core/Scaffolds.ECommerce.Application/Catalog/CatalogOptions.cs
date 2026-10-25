namespace Scaffolds.ECommerce.Application.Catalog;

public sealed class CatalogOptions
{
    public const string SectionName = "ECommerce:Catalog";

    public decimal UnitPrice { get; init; }

    public decimal DiscountRate { get; init; }
}
