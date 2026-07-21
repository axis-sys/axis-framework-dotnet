using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCatalogRepositories(this IServiceCollection services)
    {
        services.AddScoped<IProductsPort, ProductsRepository>();
        services.AddScoped<IOrdersPort, OrdersRepository>();
        services.AddScoped<ICartItemsPort, CartItemsRepository>();
        return services;
    }
}
