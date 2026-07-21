namespace Scaffolds.ECommerce.SharedKernel.ContextIds;

[ValueObject]
public readonly partial record struct OrderId
{
    public static OrderId New => new(Guid.CreateVersion7().ToString());

    private OrderId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("OrderId cannot be null or empty");

        if (!Guid.TryParse(value, out _))
            throw new ArgumentException($"'{value}' is not a valid OrderId");

        Value = value;
    }

    private string Value { get; }
}
