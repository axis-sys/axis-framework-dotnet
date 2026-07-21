using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;

namespace Scaffolds.ECommerce.Contracts.Driven.Catalog;

public interface ICartItemsPort
{
    Task<AxisResult<ICartItemEntityProperties>> GetByCartIdAsync(CartId cartId);
    Task<AxisResult<ICartItemEntityProperties>> CreateAsync(ICartItemEntityProperties cartItem);
    Task<AxisResult<ICartItemEntityProperties>> UpdateAsync(ICartItemEntityProperties cartItem);
}
