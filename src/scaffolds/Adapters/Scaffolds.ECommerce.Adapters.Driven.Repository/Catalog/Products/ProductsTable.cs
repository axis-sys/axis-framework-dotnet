namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

public static class ProductsTable
{
    public const string Table = $"{EComDbInit.Schema}.PRODUCTS";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(ProductsColumns.ProductId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(ProductsColumns.Sku, AxisDbType.Varchar(100), notNull: true)
        .Column(ProductsColumns.Name, AxisDbType.Varchar(200), notNull: true)
        .Column(ProductsColumns.Stock, AxisDbType.Int, notNull: true)
        .Unique("UQ_PRODUCTS_SKU", ProductsColumns.Sku);
}
