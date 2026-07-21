using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCustomersRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICustomersPort, CustomersRepository>();
        return services;
    }
}
