namespace Scaffolds.ECommerce.Application.Samples;

public static class PriceCalculator
{
    private const string PriceMustBePositive = "PRICE_MUST_BE_POSITIVE";

    #region scaffold:line-total
    public static AxisResult<decimal> LineTotal(AxisResult<int> quantity, AxisResult<decimal> unitPrice)
        => from qtd in quantity
           from price in unitPrice
           select qtd * price;
    #endregion

    #region scaffold:apply-discount
    public static AxisResult<decimal> ApplyDiscount(decimal price, decimal percent)
        => price.Rop()
            .Ensure(value => value > 0, AxisError.ValidationRule(PriceMustBePositive))
            .Map(value => value * (1 - percent));
    #endregion
}
