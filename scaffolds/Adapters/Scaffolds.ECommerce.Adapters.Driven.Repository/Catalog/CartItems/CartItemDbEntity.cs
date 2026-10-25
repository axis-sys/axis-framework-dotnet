using System.Data.Common;
using Scaffolds.ECommerce.Domain.Catalog.CartItems;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

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
