namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;

/// <summary>A catalog product as returned by the read use cases.</summary>
public sealed record GetProductResponse : IAxisQueryResponse
{
    /// <summary>Id of the product.</summary>
    public required string ProductId { get; init; }

    /// <summary>Display name of the product.</summary>
    public required string Name { get; init; }

    /// <summary>Units currently available in stock.</summary>
    public required int Stock { get; init; }
}
