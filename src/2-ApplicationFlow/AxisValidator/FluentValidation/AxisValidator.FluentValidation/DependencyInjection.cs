using System.Reflection;
using AxisMediator.Contracts.Pipelines;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace AxisValidator;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAxisValidator(params Assembly[] assemblies)
        {
            services.AddValidatorsFromAssemblies(assemblies.AsEnumerable(), includeInternalTypes: true);
            services.AddScoped(typeof(IAxisValidator<>), typeof(FluentValidatorAdapter<>));
            services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(ValidationBehavior<>));
            services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            return services;
        }
    }
}
