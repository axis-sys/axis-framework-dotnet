using Scaffolds.ECommerce.Adapters.Driven.InMemory.EmailValidation;

namespace Scaffolds.ECommerce.Adapters.Driven.InMemory;

public static class DependencyInjection
{
    public static IServiceCollection AddInMemoryEmail(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryEmailOutbox>();
        services.AddSingleton<IAxisEmailService, InMemoryEmailService>();
        return services;
    }
}
