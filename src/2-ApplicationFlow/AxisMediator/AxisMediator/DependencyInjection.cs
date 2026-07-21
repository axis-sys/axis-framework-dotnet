using System.Reflection;
using AxisMediator.Contracts;
using AxisMediator.Contracts.CQRS.Commands;
using AxisMediator.Contracts.CQRS.Events;
using AxisMediator.Contracts.CQRS.Handlers;
using AxisMediator.Contracts.CQRS.Queries;
using AxisMediator.Contracts.Pipelines;
using AxisMediator.Pipelines;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMediator;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAxisMediator()
        {
            services.AddScoped<IAxisMediatorHandler, AxisMediatorHandler>();
            services.AddScoped<IAxisMediator, AxisMediator>();
            services.AddSingleton<IAxisMediatorAccessor, AxisMediatorAccessor>();
            services.AddSingleton<IAxisMediatorContextAccessor, AxisMediatorContextAccessor>();
            return services;
        }

        public IServiceCollection AddPerformanceBehavior()
        {
            services.AddTransient(typeof(IAxisPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
            return services;
        }
    }

    public static IServiceCollection AddCqrsMediator(this IServiceCollection services, Assembly assembly)
    {
        RegisterHandlers(services, assembly, typeof(IAxisCommand<>));
        RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<>));
        RegisterHandlers(services, assembly, typeof(IAxisCommandHandler<,>));
        RegisterHandlers(services, assembly, typeof(IAxisQueryHandler<,>));
        RegisterHandlers(services, assembly, typeof(IAxisStreamQueryHandler<,>));
        RegisterHandlers(services, assembly, typeof(IAxisEventHandler<>));
        return services;
    }

    private static void RegisterHandlers(
        IServiceCollection services,
        Assembly assembly,
        Type handlerInterfaceType)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsGenericType: false } &&
                type.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == handlerInterfaceType))
            .Distinct();

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == handlerInterfaceType)
                .Distinct();

            foreach (var interfaceType in interfaces)
                services.AddTransient(interfaceType, handlerType);
        }
    }
}
