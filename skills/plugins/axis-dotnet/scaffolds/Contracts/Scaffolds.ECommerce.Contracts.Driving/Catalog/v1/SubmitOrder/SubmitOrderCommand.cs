namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

/// <summary>
/// Submits an order for processing. Quantity and coupon are validated together, so a single call
/// reports every violated rule at once.
/// </summary>
public sealed record SubmitOrderCommand : IAxisCommand<SubmitOrderResponse>
{
    /// <summary>Number of units in the order. Must be positive.</summary>
    /// <example>2</example>
    public int Quantity { get; init; }

    /// <summary>CartId code to the order. Required to link the current cart.</summary>
    public string? CartId { get; init; }
}
