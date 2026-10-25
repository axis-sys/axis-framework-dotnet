using Scaffolds.ECommerce.Domain.Catalog.CartItems;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;

public interface ICartItemsReaderPort
{
    Task<AxisResult<ICartItemEntityProperties>> GetByCartIdAsync(CartId cartId);
}
