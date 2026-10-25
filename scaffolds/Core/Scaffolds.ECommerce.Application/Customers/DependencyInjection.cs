using Scaffolds.ECommerce.Application.Customers.Aggregates;

namespace Scaffolds.ECommerce.Application.Customers;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCustomersServices(this IServiceCollection services)
    {
        services.AddScoped<ICustomerFactory, CustomerFactory>();
        return services;
    }
}
