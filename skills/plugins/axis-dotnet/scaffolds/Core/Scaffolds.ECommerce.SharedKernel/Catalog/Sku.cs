namespace Scaffolds.ECommerce.SharedKernel.Catalog;

#region scaffold:sku-value-object
[ValueObject]
public readonly partial record struct Sku
{
    private Sku(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty");

        Value = value.Trim();
    }

    private string Value { get; }
}
#endregion
