namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

public static class CartItemsTable
{
    public const string Table = $"{EComDbInit.Schema}.CART_ITEMS";

    // A cart holds a single pending item — CartId is the primary key, so the last checkout into a cart
    // wins (CartItemsRepository.CreateAsync/UpdateAsync implement the replace via AxisResult.RecoverConflict).
    public static AxisTable Define() => new AxisTable(Table)
        .Column(CartItemsColumns.CartId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(CartItemsColumns.ProductId, AxisDbType.Varchar(50), notNull: true)
        .Column(CartItemsColumns.Quantity, AxisDbType.Int, notNull: true);
}
