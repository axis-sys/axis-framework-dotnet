namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;

public static class OrdersColumns
{
    public const string OrderId = "ORDER_ID";
    public const string CustomerId = "CUSTOMER_ID";
    public const string ProductId = "PRODUCT_ID";
    public const string Quantity = "QUANTITY";
    public const string CartId = "CART_ID";
    
    public const string All = $"{OrderId}, {CustomerId}, {ProductId}, {Quantity}, {CartId}";
}
