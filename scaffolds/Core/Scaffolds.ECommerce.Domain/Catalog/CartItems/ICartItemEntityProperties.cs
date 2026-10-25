using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;
using Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

namespace Scaffolds.ECommerce.Domain.Catalog.CartItems;

public interface ICartItemEntityProperties
{
    CartId CartId { get; }
    ProductId ProductId { get; }
    int Quantity { get; }
}
