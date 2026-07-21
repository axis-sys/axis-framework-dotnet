using Scaffolds.ECommerce.Adapters.Driven.Repository.Auth;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Catalog;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWorkAdapter>();
        return services
            .AddCatalogRepositories()
            .AddCustomersRepositories()
            .AddAuthRepositories();
    }
}
