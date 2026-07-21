using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Axis;

/// <summary>
/// Registration helpers for the AxisResult HTTP response rendering.
/// </summary>
public static class AxisResultHttpResponseDependencyInjection
{
    /// <summary>
    /// Applies the optional <c>type</c> base URI override from configuration key
    /// <c>AxisResult:Http:ProblemTypeBaseUri</c>. When the key is absent or blank the framework default
    /// (<see cref="AxisProblemDetailsConfiguration.DefaultProblemTypeBaseUri"/>) is kept.
    /// </summary>
    /// <param name="services">The service collection (returned unchanged for chaining).</param>
    /// <param name="configuration">The application configuration.</param>
    public static IServiceCollection AddAxisResultHttpResponse(
        this IServiceCollection services, IConfiguration configuration)
    {
        AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri(
            configuration["AxisResult:Http:ProblemTypeBaseUri"]);
        return services;
    }
}
