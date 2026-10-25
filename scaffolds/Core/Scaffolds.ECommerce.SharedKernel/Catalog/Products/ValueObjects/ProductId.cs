namespace Scaffolds.ECommerce.SharedKernel.Catalog.Products.ValueObjects;

[ValueObject]
public readonly partial record struct ProductId
{
    public static ProductId New => new(Guid.CreateVersion7().ToString());

    private ProductId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProductId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid ProductId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid ProductId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
