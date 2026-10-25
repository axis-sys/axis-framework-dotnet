using Scaffolds.ECommerce.Domain.Catalog.CartItems;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;

public interface ICartItemsWritePort
{
    Task<AxisResult<ICartItemEntityProperties>> CreateAsync(ICartItemEntityProperties cartItem);
    Task<AxisResult<ICartItemEntityProperties>> UpdateAsync(ICartItemEntityProperties cartItem);
}
