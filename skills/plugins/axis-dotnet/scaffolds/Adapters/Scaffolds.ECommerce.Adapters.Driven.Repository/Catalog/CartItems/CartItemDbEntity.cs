using System.Data.Common;
using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

internal sealed record CartItemDbEntity(
    CartId CartId,
    ProductId ProductId,
    int Quantity
) : ICartItemEntityProperties
{
    internal static CartItemDbEntity FromReader(DbDataReader reader)
        => new(
            (CartId)reader.GetString(0),
            (ProductId)reader.GetString(1),
            reader.GetInt32(2)
        );
}
