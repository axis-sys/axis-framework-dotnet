using Axis.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;

namespace Axis;

public static class DependencyInjection
{
    public static IServiceCollection AddOpenTelemetryAxis(this IServiceCollection services)
    {
        services.AddSingleton<OpenTelemetryAdapter>();
        services.AddSingleton<IAxisTelemetry>(sp => sp.GetRequiredService<OpenTelemetryAdapter>());
        services.AddSingleton<IAxisMetrics>(sp => sp.GetRequiredService<OpenTelemetryAdapter>());
        return services;
    }
}
