using Testcontainers.PostgreSql;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class PostgresECommerceWebAppFactory : ECommerceWebAppFactory
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:latest")
        .WithDatabase("ecommerce_e2e").WithUsername("e2e_test").WithPassword("password_test").WithCleanUp(true).Build();

    protected override string ProviderName => "Postgres";

    protected override async Task<string> StartContainerAsync()
    {
        await _container.StartAsync();
        return _container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => _container.DisposeAsync().AsTask();
}

// DisableParallelization: InitializeAsync mutates process-wide environment variables the host reads at
// boot — running alongside the MySql collection's own InitializeAsync would race on that shared state.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresECommerceWebAppCollection : ICollectionFixture<PostgresECommerceWebAppFactory>
{
    public const string Name = "PostgresECommerceWebApp";
}
