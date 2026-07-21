namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

public static class ProductsColumns
{
    public const string ProductId = "PRODUCT_ID";
    public const string Sku = "SKU";
    public const string Name = "NAME";
    public const string Stock = "STOCK";
    
    public const string All = $"{ProductId}, {Sku}, {Name}, {Stock}";
}
