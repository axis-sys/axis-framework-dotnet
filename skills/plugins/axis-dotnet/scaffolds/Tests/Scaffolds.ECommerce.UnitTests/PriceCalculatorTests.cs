namespace Scaffolds.ECommerce.UnitTests;

public sealed class PriceCalculatorTests
{
    [Fact]
    public void MultipliesQuantityAndPriceWhenComputingLineTotal()
    {
        AxisResult<int> quantity = 3;
        AxisResult<decimal> unitPrice = 10m;

        var result = PriceCalculator.LineTotal(quantity, unitPrice);

        result.ShouldSucceedWith(30m);
    }

    [Fact]
    public void RejectsNonPositivePriceWhenApplyingDiscount()
    {
        var result = PriceCalculator.ApplyDiscount(0m, 0.1m);

        result.ShouldFailWithCode("PRICE_MUST_BE_POSITIVE");
    }
}
