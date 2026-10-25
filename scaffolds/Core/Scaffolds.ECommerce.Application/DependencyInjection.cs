using Microsoft.Extensions.DependencyInjection.Extensions;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Application.Authorization;
using Scaffolds.ECommerce.Application.Catalog;
using Scaffolds.ECommerce.Application.Customers;
using Scaffolds.ECommerce.Application.EmailValidation;
using Scaffolds.ECommerce.Application.Pipelines;

namespace Scaffolds.ECommerce.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceApplication(this IServiceCollection services)
    {
        services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ActorStampBehavior<,>));
        services.TryAddSingleton(TimeProvider.System);
        var assembly = typeof(DependencyInjection).Assembly;

        return services
            .AddAuthServices()
            .AddAuthorizationServices()
            .AddCatalogServices()
            .AddCustomersServices()
            .AddEmailValidationServices()
            .AddCqrsMediator(assembly)
            .AddAxisValidator(assembly)
            .AddAxisSagaHandlers(assembly);
    }
}
