using System.Data.Common;
using Scaffolds.ECommerce.Domain.Catalog.Orders;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;

internal sealed record OrderDbEntity(
    OrderId OrderId,
    CustomerId CustomerId,
    ProductId ProductId,
    int Quantity,
    CartId CartId
) : IOrderEntityProperties
{
    internal static OrderDbEntity FromReader(DbDataReader reader)
        => new(
            (OrderId)reader.GetString(0),
            (CustomerId)reader.GetString(1),
            (ProductId)reader.GetString(2),
            reader.GetInt32(3),
            (CartId)reader.GetString(4)
        );
}
