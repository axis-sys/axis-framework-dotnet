namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;

public static class OrdersTable
{
    public const string Table = $"{EComDbInit.Schema}.ORDERS";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(OrdersColumns.OrderId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(OrdersColumns.CustomerId, AxisDbType.Varchar(50), notNull: true)
        .Column(OrdersColumns.ProductId, AxisDbType.Varchar(50), notNull: true)
        .Column(OrdersColumns.Quantity, AxisDbType.Int, notNull: true)
        .Column(OrdersColumns.CartId, AxisDbType.Varchar(50), notNull: true);
}
