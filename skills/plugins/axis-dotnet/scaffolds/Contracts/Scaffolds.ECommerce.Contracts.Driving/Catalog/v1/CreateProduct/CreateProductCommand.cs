namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.CreateProduct;

/// <summary>
/// Creates a new product in the catalog. Strict variant: a repeated SKU fails with a conflict (409).
/// Contrast with RegisterProductCommand, the idempotent variant, where a repeated SKU returns the
/// existing product instead of failing.
/// </summary>
public sealed record CreateProductCommand : IAxisCommand<CreateProductResponse>
{
    /// <summary>Unique stock-keeping unit of the product.</summary>
    /// <example>SKU-KEYBOARD-01</example>
    public string? Sku { get; init; }

    /// <summary>Display name of the product.</summary>
    /// <example>Mechanical Keyboard</example>
    public string? Name { get; init; }

    /// <summary>Units available when the product enters the catalog.</summary>
    /// <example>25</example>
    public int InitialStock { get; init; }
}
