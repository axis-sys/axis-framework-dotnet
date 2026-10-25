using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Shared;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.Orders;

public interface IOrderEntityProperties
{
    OrderId OrderId { get; }
    CustomerId CustomerId { get; }
    ProductId ProductId { get; }
    int Quantity { get; }
    CartId CartId { get; }
}
