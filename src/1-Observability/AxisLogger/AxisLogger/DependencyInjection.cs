using AxisMediator.Contracts.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace Axis;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAxisLogger()
        {
            services.AddLogging();
            services.AddScoped(typeof(IAxisLogger<>), typeof(AxisLogger<>));
            return services;
        }

        public IServiceCollection AddLoggingBehavior()
        {
            services.AddSingleton(TimeProvider.System);
            services.AddTransient(typeof(IAxisPipelineBehavior<>), typeof(LoggingBehavior<>));
            services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            return services;
        }

    }
}
