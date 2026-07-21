namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;

/// <summary>Identifier of the registered product.</summary>
public sealed record RegisterProductResponse : IAxisCommandResponse
{
    /// <summary>Id of the product that entered the catalog.</summary>
    public required string ProductId { get; init; }
}
