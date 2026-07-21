namespace Scaffolds.ECommerce.Contracts.Driven.Catalog;

public interface IOrdersPort
{
    Task<AxisResult> CreateAsync(IOrderEntityProperties order);
}
