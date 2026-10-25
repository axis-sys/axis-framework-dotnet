namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;

public static class CartItemsColumns
{
    public const string CartId = "CART_ID";
    public const string ProductId = "PRODUCT_ID";
    public const string Quantity = "QUANTITY";
    
    public const string All = $"{CartId}, {ProductId}, {Quantity}";
}
