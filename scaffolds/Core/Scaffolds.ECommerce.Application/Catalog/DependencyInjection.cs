using Scaffolds.ECommerce.Application.Catalog.CartItems.Aggregates;
using Scaffolds.ECommerce.Application.Catalog.Orders.Aggregates;
using Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPresenter;
using Scaffolds.ECommerce.Application.Catalog.Orders.Services.OrderPricing;
using Scaffolds.ECommerce.Application.Catalog.Products.Aggregates;

namespace Scaffolds.ECommerce.Application.Catalog;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        services.AddScoped<IProductFactory, ProductFactory>();
        services.AddScoped<IOrderFactory, OrderFactory>();
        services.AddScoped<ICartItemFactory, CartItemFactory>();
        services.AddScoped<IOrderPricingService, OrderPricingService>();
        services.AddScoped<IOrderPresenterService, OrderPresenterService>();
        return services;
    }
}
