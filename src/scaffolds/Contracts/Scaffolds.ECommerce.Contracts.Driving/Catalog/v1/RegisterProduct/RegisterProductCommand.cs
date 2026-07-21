namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.RegisterProduct;

/// <summary>
/// Registers a product in the catalog. Idempotent variant: a repeated SKU returns the existing product
/// (recovers from the conflict via AxisResult.RecoverConflict). Contrast with CreateProductCommand, the
/// strict variant, where a repeated SKU fails with a conflict.
/// </summary>
public sealed record RegisterProductCommand : IAxisCommand<RegisterProductResponse>
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
