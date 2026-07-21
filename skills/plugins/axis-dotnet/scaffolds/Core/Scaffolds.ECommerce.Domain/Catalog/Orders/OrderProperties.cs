using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;
using OrderId = Scaffolds.ECommerce.SharedKernel.ContextIds.OrderId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Domain.Catalog.Orders;

internal sealed record OrderProperties(
    OrderId OrderId,
    CustomerId CustomerId,
    ProductId ProductId,
    int Quantity,
    CartId CartId
) : IOrderEntityProperties;
