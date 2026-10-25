using Scaffolds.ECommerce.Domain.Catalog.Orders;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;

public interface IOrdersReaderPort
{
    Task<AxisResult<IOrderEntityProperties>> GetByIdAsync(OrderId orderId);
}
