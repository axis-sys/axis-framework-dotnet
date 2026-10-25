using Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;
using Scaffolds.ECommerce.Domain.Catalog.CartItems;
using Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

internal sealed class CartItemsRepository(IAxisDbRepository db) : ICartItemsReaderPort, ICartItemsWritePort
{
    private const string Select = $"SELECT {CartItemsColumns.All}";

    public Task<AxisResult<ICartItemEntityProperties>> GetByCartIdAsync(CartId cartId)
        => db.GetAsync<ICartItemEntityProperties>(
            $"{Select} FROM {CartItemsTable.Table} WHERE {CartItemsColumns.CartId} = @cartId",
            b => b.Add("cartId", cartId.ToString()),
            CartItemDbEntity.FromReader,
            CartItemErrors.CartItemNotFound.Code);

    public Task<AxisResult<ICartItemEntityProperties>> CreateAsync(ICartItemEntityProperties cartItem)
        => db.ExecuteAsync(
                $"INSERT INTO {CartItemsTable.Table} ({CartItemsColumns.All}) VALUES (@cartId, @productId, @quantity)",
                b => b.Add("cartId", cartItem.CartId.ToString())
                    .Add("productId", cartItem.ProductId.ToString())
                    .Add("quantity", cartItem.Quantity),
                duplicateKeyCode: CartItemErrors.CartItemAlreadyExists.Code)
            .WithValueAsync(cartItem);

    public Task<AxisResult<ICartItemEntityProperties>> UpdateAsync(ICartItemEntityProperties cartItem)
        => db.ExecuteAsync(
                $"UPDATE {CartItemsTable.Table} SET {CartItemsColumns.ProductId} = @productId, {CartItemsColumns.Quantity} = @quantity WHERE {CartItemsColumns.CartId} = @cartId",
                b => b.Add("productId", cartItem.ProductId.ToString())
                    .Add("quantity", cartItem.Quantity)
                    .Add("cartId", cartItem.CartId.ToString()))
            .WithValueAsync(cartItem);
}
