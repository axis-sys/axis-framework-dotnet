namespace Scaffolds.ECommerce.SharedKernel.Catalog.CartItems.ValueObjects;

[ValueObject]
public readonly partial record struct CartId
{
    public static CartId New => new(Guid.CreateVersion7().ToString());

    private CartId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CartId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid CartId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid CartId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
