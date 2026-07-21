namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Checkout;

/// <summary>Identifiers of the order opened by the checkout.</summary>
public sealed record CheckoutResponse : IAxisCommandResponse
{
    /// <summary>Id of the authenticated customer the order belongs to.</summary>
    public required AxisEntityId Customer { get; init; }

    /// <summary>Id of the product whose stock was reserved.</summary>
    public required string ProductId { get; init; }
}
