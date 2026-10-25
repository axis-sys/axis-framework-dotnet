using Scaffolds.ECommerce.Adapters.Driving.Facade.Admin;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Auth;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Authorization;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog.Orders;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Catalog.Products;
using Scaffolds.ECommerce.Adapters.Driving.Facade.Customers;
using Scaffolds.ECommerce.Adapters.Driving.Facade.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceFacade(this IServiceCollection services)
    {
        services.AddAxisMediator();
        services.AddScoped<IProductsFacade, ProductsFacade>();
        services.AddScoped<IOrdersFacade, OrdersFacade>();
        services.AddScoped<ICustomersFacade, CustomersFacade>();
        services.AddScoped<IAuthFacade, AuthFacade>();
        services.AddScoped<IEmailValidationFacade, EmailValidationFacade>();
        services.AddScoped<IAdminFacade, AdminFacade>();
        services.AddScoped<IAuthorizationFacade, AuthorizationFacade>();
        return services;
    }
}
