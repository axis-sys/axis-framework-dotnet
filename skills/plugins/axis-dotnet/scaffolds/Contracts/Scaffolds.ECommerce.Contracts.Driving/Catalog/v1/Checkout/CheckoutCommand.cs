namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;

/// <summary>
/// Reserves stock for a product and opens an order for the authenticated customer in a single step.
/// Fails with a business rule when the requested quantity exceeds the available stock.
/// </summary>
public sealed record CheckoutCommand : IAxisCommand<CheckoutResponse>
{
    /// <summary>Id of the cupom opened for the reservation.</summary>
    /// <example>af92f3a1-fb9c-ad61-9e5f-3c1051b60001</example>
    public required string CartId { get; init; }

    /// <summary>Id of the product being checked out. Injected from the route, never from the body.</summary>
    /// <example>0192f3a1-7b3c-7d21-9e5f-3c8a51b60001</example>
    [JsonIgnore]
    public string? ProductId { get; init; }

    /// <summary>Number of units to reserve. Must not exceed the product's available stock.</summary>
    /// <example>2</example>
    public int Quantity { get; init; }
}
