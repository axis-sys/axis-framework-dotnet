using Scaffolds.ECommerce.Domain.Catalog.Orders;

namespace Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPresenter;

internal interface IOrderPresenterService
{
    string Describe(IOrderEntityProperties order, AxisResult<decimal> total);
    string Summary(IOrderEntityProperties order, AxisResult<decimal> total);
}
