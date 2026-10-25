namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.CreateProduct;

public sealed record CreateProductCommand : IAxisCommand<CreateProductResponse>
{
    public string? Sku { get; init; }

    public string? Name { get; init; }

    public int? InitialStock { get; init; }
}
