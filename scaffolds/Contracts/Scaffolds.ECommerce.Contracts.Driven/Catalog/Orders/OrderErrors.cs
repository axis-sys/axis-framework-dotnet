namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;

public static class OrderErrors
{
    public static readonly AxisError OrderAlreadyExists = AxisError.Conflict("ORDER_ALREADY_EXISTS");

    public const string OrderIdInvalid = "ORDER_ID_INVALID";
    public static readonly AxisError OrderNotFound = AxisError.NotFound("ORDER_NOT_FOUND");
    public static readonly AxisError PriceMustBePositive = AxisError.ValidationRule("PRICE_MUST_BE_POSITIVE");
}
