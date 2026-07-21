using Testcontainers.MySql;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public sealed class MySqlECommerceWebAppFactory : ECommerceWebAppFactory
{
    private readonly MySqlContainer _container = new MySqlBuilder("mysql:8.4")
        .WithDatabase("ecommerce_e2e").WithUsername("root").WithPassword("password_test").WithCleanUp(true).Build();

    protected override string ProviderName => "MySql";

    protected override async Task<string> StartContainerAsync()
    {
        await _container.StartAsync();
        return _container.GetConnectionString();
    }

    protected override Task StopContainerAsync() => _container.DisposeAsync().AsTask();
}

// DisableParallelization: InitializeAsync mutates process-wide environment variables the host reads at
// boot — running alongside the Postgres collection's own InitializeAsync would race on that shared state.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MySqlECommerceWebAppCollection : ICollectionFixture<MySqlECommerceWebAppFactory>
{
    public const string Name = "MySqlECommerceWebApp";
}
