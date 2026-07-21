using System.Data.Common;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;
using OrderId = Scaffolds.ECommerce.SharedKernel.ContextIds.OrderId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

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
