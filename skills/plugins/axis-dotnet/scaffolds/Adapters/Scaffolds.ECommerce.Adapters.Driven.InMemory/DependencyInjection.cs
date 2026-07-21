using Scaffolds.ECommerce.Adapters.Driven.InMemory.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.InMemory;

public static class DependencyInjection
{
    // The email outbox stays in-memory: it is a test double that captures sent mail for assertions, not
    // business state, so it carries none of the durability/integrity concerns that moved Products, Orders,
    // Customers, CartItems, ValidationCodes and the saga runtime to real repository/Postgres/MySql storage.
    public static IServiceCollection AddInMemoryEmail(this IServiceCollection services)
    {
        services.AddSingleton<InMemoryEmailOutbox>();
        services.AddSingleton<IAxisEmailService, InMemoryEmailService>();
        return services;
    }
}
