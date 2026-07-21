using System.Reflection;
using Axis;
using AxisMediator;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMemoryBus;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisMemoryBus(this IServiceCollection services)
    {
        services.AddCqrsMediator(Assembly.GetExecutingAssembly());
        services.AddScoped<IAxisBus, MemoryBusAdapter>();
        return services;
    }
}
