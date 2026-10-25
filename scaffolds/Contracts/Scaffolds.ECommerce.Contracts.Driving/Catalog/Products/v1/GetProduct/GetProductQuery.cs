namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1.GetProduct;

public sealed record GetProductQuery : IAxisQuery<GetProductResponse>
{
    [JsonIgnore]
    public string? ProductId { get; init; }
}
