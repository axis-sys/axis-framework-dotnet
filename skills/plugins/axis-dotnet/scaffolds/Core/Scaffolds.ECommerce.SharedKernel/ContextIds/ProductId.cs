namespace Scaffolds.ECommerce.SharedKernel.ContextIds;

#region scaffold:product-id-value-object
[ValueObject]
public readonly partial record struct ProductId
{
    public static ProductId New => new(Guid.CreateVersion7().ToString());

    private ProductId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("ProductId cannot be null or empty");

        if (!Guid.TryParse(value, out _))
            throw new ArgumentException($"'{value}' is not a valid ProductId");

        Value = value;
    }

    private string Value { get; }
}
#endregion
