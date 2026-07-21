using Scaffolds.ECommerce.Adapters.Driving.Facade.Auth;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade;

public static class DependencyInjection
{
    // The single DI extension that registers the mediator plus every facade as scoped
    // (architecture-facade-pattern); the application extension registers only its own handlers.
    public static IServiceCollection AddECommerceFacade(this IServiceCollection services)
    {
        services.AddAxisMediator();
        services.AddScoped<ICatalogFacade, CatalogFacade>();
        services.AddScoped<ICustomersFacade, CustomersFacade>();
        services.AddScoped<IAuthFacade, AuthFacade>();
        services.AddScoped<IEmailValidationFacade, EmailValidationFacade>();
        return services;
    }
}
