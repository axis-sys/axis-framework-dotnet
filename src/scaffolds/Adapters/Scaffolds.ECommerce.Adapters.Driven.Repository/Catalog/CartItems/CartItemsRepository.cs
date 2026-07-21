using CartId = Scaffolds.ECommerce.SharedKernel.ContextIds.CartId;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

internal sealed class CartItemsRepository(IAxisDbRepository db) : ICartItemsPort
{
    private const string Select = $"SELECT {CartItemsColumns.All}";

    public Task<AxisResult<ICartItemEntityProperties>> GetByCartIdAsync(CartId cartId)
        => db.GetAsync<ICartItemEntityProperties>(
            $"{Select} FROM {CartItemsTable.Table} WHERE {CartItemsColumns.CartId} = @cartId",
            b => b.Add("cartId", cartId.ToString()),
            CartItemDbEntity.FromReader,
            "CART_ITEM_NOT_FOUND");

    public Task<AxisResult<ICartItemEntityProperties>> CreateAsync(ICartItemEntityProperties cartItem)
        => db.ExecuteAsync(
                $"INSERT INTO {CartItemsTable.Table} ({CartItemsColumns.All}) VALUES (@cartId, @productId, @quantity)",
                b => b.Add("cartId", cartItem.CartId.ToString())
                    .Add("productId", cartItem.ProductId.ToString())
                    .Add("quantity", cartItem.Quantity),
                duplicateKeyCode: "CART_ITEM_ALREADY_EXISTS")
            .WithValueAsync(cartItem);

    public Task<AxisResult<ICartItemEntityProperties>> UpdateAsync(ICartItemEntityProperties cartItem)
        => db.ExecuteAsync(
                $"UPDATE {CartItemsTable.Table} SET {CartItemsColumns.ProductId} = @productId, {CartItemsColumns.Quantity} = @quantity WHERE {CartItemsColumns.CartId} = @cartId",
                b => b.Add("productId", cartItem.ProductId.ToString())
                    .Add("quantity", cartItem.Quantity)
                    .Add("cartId", cartItem.CartId.ToString()))
            .WithValueAsync(cartItem);
}
