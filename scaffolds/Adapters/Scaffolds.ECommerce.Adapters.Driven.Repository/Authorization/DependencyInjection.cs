using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization;

internal static class DependencyInjection
{
    internal static IServiceCollection AddAuthorizationRepositories(this IServiceCollection services)
    {
        services.AddScoped<MembersRepository>();
        services.AddScoped<IMembersReaderPort>(provider => provider.GetRequiredService<MembersRepository>());
        services.AddScoped<IMembersWritePort>(provider => provider.GetRequiredService<MembersRepository>());
        return services;
    }
}
