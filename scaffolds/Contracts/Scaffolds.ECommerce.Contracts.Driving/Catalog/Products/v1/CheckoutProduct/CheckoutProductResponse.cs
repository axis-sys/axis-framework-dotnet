namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CheckoutProduct;

/// <summary>Identifiers of the checkout: the customer and the product whose stock was reserved.</summary>
public sealed record CheckoutProductResponse : IAxisCommandResponse
{
    /// <summary>Id of the authenticated customer the order belongs to.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Id of the product whose stock was reserved.</summary>
    public required string ProductId { get; init; }
}
