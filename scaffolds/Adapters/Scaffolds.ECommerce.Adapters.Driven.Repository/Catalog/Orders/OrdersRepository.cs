using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Domain.Catalog.Orders;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;

internal sealed class OrdersRepository(IAxisDbRepository db) : IOrdersReaderPort, IOrdersWritePort
{
    private const string Select = $"SELECT {OrdersColumns.All}";

    public Task<AxisResult<IOrderEntityProperties>> GetByIdAsync(OrderId orderId)
        => db.GetAsync<IOrderEntityProperties>(
            $"{Select} FROM {OrdersTable.Table} WHERE {OrdersColumns.OrderId} = @orderId",
            b => b.Add("orderId", orderId.ToString()),
            OrderDbEntity.FromReader,
            OrderErrors.OrderNotFound.Code);

    public Task<AxisResult> CreateAsync(IOrderEntityProperties order)
        => db.ExecuteAsync(
            $"INSERT INTO {OrdersTable.Table} ({OrdersColumns.All}) VALUES (@orderId, @customerId, @productId, @quantity, @cartId)",
            b => b.Add("orderId", order.OrderId.ToString())
                .Add("customerId", order.CustomerId.ToString())
                .Add("productId", order.ProductId.ToString())
                .Add("quantity", order.Quantity)
                .Add("cartId", order.CartId.ToString()),
            duplicateKeyCode: OrderErrors.OrderAlreadyExists.Code);
}
