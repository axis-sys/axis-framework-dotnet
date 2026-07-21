namespace Scaffolds.ECommerce.SharedKernel.ContextIds;

[ValueObject]
public readonly partial record struct CustomerId
{
    public static CustomerId New => new(Guid.CreateVersion7().ToString());

    private CustomerId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CustomerId cannot be null or empty");

        if (!Guid.TryParse(value, out _))
            throw new ArgumentException($"'{value}' is not a valid CustomerId");

        Value = value;
    }
    
    public static implicit operator CustomerId(AxisEntityId? value) => new(value);

    private string Value { get; }
}
