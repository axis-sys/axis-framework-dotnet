namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;

public sealed record CreateProductResponse : IAxisCommandResponse
{
    public required string ProductId { get; init; }
}
