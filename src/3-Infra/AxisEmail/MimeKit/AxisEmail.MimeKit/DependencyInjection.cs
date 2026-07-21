using System.Diagnostics.CodeAnalysis;
using Axis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AxisEmail.MimeKit;

[ExcludeFromCodeCoverage]
public static class EmailDependencyInjection
{
    public static IServiceCollection AddAxisMimeKitEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AxisEmailSettings>(configuration.GetSection("Axis:Email:Settings"));
        services.AddScoped<IAxisEmailService, AxisEmailService>();
        return services;
    }
}
