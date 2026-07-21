using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;
using ProductId = Scaffolds.ECommerce.SharedKernel.ContextIds.ProductId;

namespace Scaffolds.ECommerce.Domain.Catalog.CartItems;

public interface ICartItemEntityProperties
{
    CartId CartId { get; }
    ProductId ProductId { get; }
    int Quantity { get; }
}
