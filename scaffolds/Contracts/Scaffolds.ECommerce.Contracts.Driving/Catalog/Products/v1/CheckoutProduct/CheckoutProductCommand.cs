namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;

/// <summary>
/// Reserves stock of a product for a cart of the authenticated customer; the order is submitted afterwards.
/// Fails with a business rule when the requested quantity exceeds the available stock.
/// </summary>
public sealed record CheckoutProductCommand : IAxisCommand<CheckoutProductResponse>
{
    /// <summary>Id of the cart the product is checked out into.</summary>
    /// <example>0192f3a1-fb9c-7d61-9e5f-3c1051b60001</example>
    public string? CartId { get; init; }

    [JsonIgnore]
    public string? ProductId { get; init; }

    /// <summary>Number of units to reserve. Must not exceed the product's available stock.</summary>
    /// <example>2</example>
    public int? Quantity { get; init; }
}
