using Scaffolds.ECommerce.Application.Catalog;

namespace Scaffolds.ECommerce.Host.Catalog;

internal static class DependencyInjection
{
    internal static IServiceCollection AddECommerceCatalog(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<CatalogOptions>()
            .Bind(configuration.GetSection(CatalogOptions.SectionName));

        return services;
    }
}
