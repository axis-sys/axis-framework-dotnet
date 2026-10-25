namespace Scaffolds.ECommerce.SharedKernel.Catalog.Orders.ValueObjects;

[ValueObject]
public readonly partial record struct OrderId
{
    public static OrderId New => new(Guid.CreateVersion7().ToString());

    private OrderId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid OrderId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid OrderId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
