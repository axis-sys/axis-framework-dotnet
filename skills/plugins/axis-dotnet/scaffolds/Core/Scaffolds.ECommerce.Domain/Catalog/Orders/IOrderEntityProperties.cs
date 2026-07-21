using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;
using OrderId = Scaffolds.ECommerce.SharedKernel.ContextIds.OrderId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Domain.Catalog.Orders;

public interface IOrderEntityProperties
{
    OrderId OrderId { get; }
    CustomerId CustomerId { get; }
    ProductId ProductId { get; }
    int Quantity { get; }
    CartId CartId { get; }
}
