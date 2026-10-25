namespace Scaffolds.ECommerce.UnitTests;

public sealed class SkuTests
{
    [Fact]
    public void KeepsTheUpperCaseCanonicalFormWhenTypedInLowerCase()
    {
        var sku = (Sku)"  sku-42a ";

        Assert.Equal("SKU-42A", sku.ToString());
    }

    [Fact]
    public void EqualsTheSameSkuWhenOnlyTheCaseDiffers()
    {
        var lower = (Sku)"sku-42a";

        var upper = (Sku)"SKU-42A";

        Assert.Equal(lower, upper);
        Assert.Equal(lower.ToString(), upper.ToString());
    }

    [Fact]
    public void RefusesToParseWhenTheSkuIsBlank()
    {
        var parsed = Sku.TryParse("   ", out _);

        Assert.False(parsed);
    }
}
