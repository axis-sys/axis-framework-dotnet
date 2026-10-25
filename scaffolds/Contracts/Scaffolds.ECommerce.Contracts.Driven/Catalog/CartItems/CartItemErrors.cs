namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;

public static class CartItemErrors
{
    public static readonly AxisError CartItemAlreadyExists = AxisError.Conflict("CART_ITEM_ALREADY_EXISTS");
    public static readonly AxisError CartItemNotFound = AxisError.NotFound("CART_ITEM_NOT_FOUND");

    public static readonly AxisError ProductCheckedOutEventMalformed = AxisError.ValidationRule("PRODUCT_CHECKED_OUT_EVENT_MALFORMED");
}
