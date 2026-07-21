using Microsoft.Extensions.DependencyInjection.Extensions;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Application.EmailValidation;
using Scaffolds.ECommerce.Application.Pipelines;

namespace Scaffolds.ECommerce.Application;

public static class DependencyInjection
{
    // The application registers only its own behavior: handler scan and pipeline behaviors.
    // The mediator runtime and the facades are wired by the facade driving adapter
    // (Scaffolds.ECommerce.Adapters.Driving.Facade), the one doorway into these handlers.
    public static IServiceCollection AddECommerceApplication(this IServiceCollection services)
    {
        services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ActorStampBehavior<,>));
        services.TryAddSingleton(TimeProvider.System);
        var assembly = typeof(DependencyInjection).Assembly;
        
        return services
            .AddAuthServices()
            .AddEmailValidationServices()
            .AddCqrsMediator(assembly)
            .AddAxisValidator(assembly)
            .AddAxisSagaHandlers(assembly);
    }
}
