namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.SubmitOrder;

/// <summary>
/// Submits an order for the product checked out into a cart. Quantity and cart are validated together, so
/// a single call reports every violated rule at once.
/// </summary>
public sealed record SubmitOrderCommand : IAxisCommand<SubmitOrderResponse>
{
    /// <summary>Number of units in the order. Must be positive.</summary>
    /// <example>2</example>
    public int? Quantity { get; init; }

    /// <summary>Id of the cart whose checked-out product the order is placed for.</summary>
    /// <example>0192f3a1-fb9c-7d61-9e5f-3c1051b60001</example>
    public string? CartId { get; init; }
}
