using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Shared;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.Orders;

internal sealed record OrderProperties(
    OrderId OrderId,
    CustomerId CustomerId,
    ProductId ProductId,
    int Quantity,
    CartId CartId
) : IOrderEntityProperties;
