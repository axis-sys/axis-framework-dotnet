namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;

internal sealed class OrdersRepository(IAxisDbRepository db) : IOrdersPort
{
    public Task<AxisResult> CreateAsync(IOrderEntityProperties order)
        => db.ExecuteAsync(
            $"INSERT INTO {OrdersTable.Table} ({OrdersColumns.All}) VALUES (@orderId, @customerId, @productId, @quantity, @cartId)",
            b => b.Add("orderId", order.OrderId.ToString())
                .Add("customerId", order.CustomerId.ToString())
                .Add("productId", order.ProductId.ToString())
                .Add("quantity", order.Quantity)
                .Add("cartId", order.CartId.ToString()),
            duplicateKeyCode: "ORDER_ALREADY_EXISTS");
}
