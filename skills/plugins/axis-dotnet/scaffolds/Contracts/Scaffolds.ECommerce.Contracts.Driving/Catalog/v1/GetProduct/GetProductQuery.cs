namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.GetProduct;

/// <summary>Reads a single product by its id.</summary>
public sealed record GetProductQuery : IAxisQuery<GetProductResponse>
{
    /// <summary>Id of the product to read. Injected from the route, never from the body.</summary>
    /// <example>0192f3a1-7b3c-7d21-9e5f-3c8a51b60001</example>
    [JsonIgnore]
    public string? ProductId { get; init; }
}
