namespace Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

[ValueObject]
public readonly partial record struct Sku
{
    private Sku(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty");

        Value = value.Trim().ToUpperInvariant();
    }

    private string Value { get; }
}
