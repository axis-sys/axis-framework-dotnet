using Scaffolds.ECommerce.Domain.Catalog.Orders;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPricing;

internal interface IOrderPricingService
{
    AxisResult<decimal> Price(IOrderEntityProperties order);
}
