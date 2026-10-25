using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.CartItems;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Orders;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog.Products;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.CartItems;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Orders;
using Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCatalogRepositories(this IServiceCollection services)
    {
        services.AddScoped<ProductsRepository>();
        services.AddScoped<IProductsReaderPort>(provider => provider.GetRequiredService<ProductsRepository>());
        services.AddScoped<IProductsWritePort>(provider => provider.GetRequiredService<ProductsRepository>());

        services.AddScoped<OrdersRepository>();
        services.AddScoped<IOrdersReaderPort>(provider => provider.GetRequiredService<OrdersRepository>());
        services.AddScoped<IOrdersWritePort>(provider => provider.GetRequiredService<OrdersRepository>());

        services.AddScoped<CartItemsRepository>();
        services.AddScoped<ICartItemsReaderPort>(provider => provider.GetRequiredService<CartItemsRepository>());
        services.AddScoped<ICartItemsWritePort>(provider => provider.GetRequiredService<CartItemsRepository>());
        return services;
    }
}
