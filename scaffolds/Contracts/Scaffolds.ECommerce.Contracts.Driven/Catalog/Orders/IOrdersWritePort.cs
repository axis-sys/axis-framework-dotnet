using Scaffolds.ECommerce.Domain.Catalog.Orders;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;

public interface IOrdersWritePort
{
    Task<AxisResult> CreateAsync(IOrderEntityProperties order);
}
