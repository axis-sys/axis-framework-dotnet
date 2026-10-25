namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

public static class CartItemsTable
{
    public const string Table = $"{CatalogDbInit.Schema}.CART_ITEMS";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(CartItemsColumns.CartId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(CartItemsColumns.ProductId, AxisDbType.Varchar(50), notNull: true)
        .Column(CartItemsColumns.Quantity, AxisDbType.Int, notNull: true);
}
