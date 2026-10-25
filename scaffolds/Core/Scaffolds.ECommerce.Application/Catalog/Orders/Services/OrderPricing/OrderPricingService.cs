using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Domain.Catalog.Orders;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPricing;

internal sealed class OrderPricingService(IOptions<CatalogOptions> catalog) : IOrderPricingService
{
    public AxisResult<decimal> Price(IOrderEntityProperties order)
        => LineTotal(order.Quantity, catalog.Value.UnitPrice)
            .Then(lineTotal => ApplyDiscount(lineTotal, catalog.Value.DiscountRate));

    private static AxisResult<decimal> LineTotal(AxisResult<int> quantity, AxisResult<decimal> unitPrice)
        => from units in quantity
           from price in unitPrice
           select units * price;

    private static AxisResult<decimal> ApplyDiscount(decimal price, decimal rate)
        => price.Rop()
            .Ensure(value => value > 0, OrderErrors.PriceMustBePositive)
            .Map(value => value * (1 - rate));
}
