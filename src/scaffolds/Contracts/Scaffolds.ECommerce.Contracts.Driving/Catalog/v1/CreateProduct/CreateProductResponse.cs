namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;

/// <summary>Identifier of the created product.</summary>
public sealed record CreateProductResponse : IAxisCommandResponse
{
    /// <summary>Id of the product that entered the catalog.</summary>
    public required string ProductId { get; init; }
}
