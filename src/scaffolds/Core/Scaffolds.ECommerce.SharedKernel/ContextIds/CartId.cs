namespace Scaffolds.ECommerce.SharedKernel.ContextIds;

[ValueObject]
public readonly partial record struct CartId
{
    public static CartId New => new(Guid.CreateVersion7().ToString());

    private CartId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("CartId cannot be null or empty");

        if (!Guid.TryParse(value, out _))
            throw new ArgumentException($"'{value}' is not a valid CartId");

        Value = value;
    }

    private string Value { get; }
}
