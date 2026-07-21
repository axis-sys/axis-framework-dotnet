using Axis;
using Microsoft.Extensions.DependencyInjection;

namespace AxisMemoryCache;

public static class DependencyInjection
{
    public static IServiceCollection AddAxisMemoryCache(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IAxisCache, MemoryCacheAdapter>();
        return services;
    }
}
