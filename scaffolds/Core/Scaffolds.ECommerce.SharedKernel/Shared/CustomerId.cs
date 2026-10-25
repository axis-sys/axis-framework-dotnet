namespace Scaffolds.ECommerce.SharedKernel.Shared;

[ValueObject]
public readonly partial record struct CustomerId
{
    public static CustomerId New => new(Guid.CreateVersion7().ToString());

    private CustomerId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CustomerId cannot be null or empty");

        if (!Guid.TryParse(value, out var guid))
            throw new ArgumentException($"'{value}' is not a valid CustomerId");

        if (guid.Version != 7)
            throw new ArgumentException($"'{value}' is not a valid CustomerId (version 7 expected)");

        Value = value;
    }

    private string Value { get; }
}
