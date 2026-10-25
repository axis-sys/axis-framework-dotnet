using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;

internal static class DependencyInjection
{
    internal static IServiceCollection AddCustomersRepositories(this IServiceCollection services)
    {
        services.AddScoped<CustomersRepository>();
        services.AddScoped<ICustomersReaderPort>(provider => provider.GetRequiredService<CustomersRepository>());
        services.AddScoped<ICustomersWritePort>(provider => provider.GetRequiredService<CustomersRepository>());
        return services;
    }
}
