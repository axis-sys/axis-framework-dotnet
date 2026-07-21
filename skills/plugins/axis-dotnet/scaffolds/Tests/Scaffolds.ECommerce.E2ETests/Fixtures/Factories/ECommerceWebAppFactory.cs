using System.Collections.Concurrent;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

// The DB-backed factory (testing-e2e-hermetic-provider-pinning): owns a real Testcontainers database and
// points the connection string at it via an environment variable set in InitializeAsync (nulled on
// DisposeAsync). Program.cs reads builder.Configuration for the connection string before builder.Build(),
// at a point ConfigureWebHost's own config layering has not yet reached — only the process environment
// (populated by WebApplicationBuilder.CreateBuilder's default AddEnvironmentVariables()) is visible that
// early, hence the environment variable rather than ConfigureAppConfiguration. No real network, no real
// identity provider either way: the external-provider schemes are swapped for TestAuthHandler, and tokens
// are obtained through the REAL login flow.
public abstract class ECommerceWebAppFactory : WebApplicationFactory<Host.Program>, IAsyncLifetime
{
    public ConcurrentQueue<string> ServerErrors { get; } = new();

    private readonly Dictionary<EUserType, string> _accessTokenCache = [];

    protected abstract string ProviderName { get; }
    protected abstract Task<string> StartContainerAsync();
    protected abstract Task StopContainerAsync();

    public async ValueTask InitializeAsync()
    {
        var connectionString = await StartContainerAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__ECommerce", connectionString);
        Environment.SetEnvironmentVariable("Database__Provider", ProviderName);
        // Forces the host (and Program.cs's own migration run) to build now, while the environment
        // variables above are still set — CreateClient()/Services build the host once and reuse it.
        using var warmup = CreateClient();
    }

    public override async ValueTask DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__ECommerce", null);
        Environment.SetEnvironmentVariable("Database__Provider", null);
        await StopContainerAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(new CollectingLoggerProvider(ServerErrors)));

        // Boot configuration: values the host needs before builder.Build() — token options and the
        // bootstrap-admin external id the SystemAdmin test user is promoted from.
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ECommerce:Auth:Token:Issuer"] = "https://scaffolds-ecommerce.test",
            ["ECommerce:Auth:Token:Audience"] = "scaffolds-ecommerce-e2e",
            ["ECommerce:Auth:Token:AccessTokenLifetimeSeconds"] = "300",
            ["ECommerce:Auth:Token:SigningKey"] = "e2e-only-signing-key-not-for-prod-0123456789",
            ["ECommerce:Customers:BootstrapAdminExternalIds:0"] = TestUsers.SystemAdminExternalId,
        }));

        builder.ConfigureTestServices(services =>
        {
            services.AddTransient<TestAuthHandler>();
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                foreach (var schemeName in new[] { AuthSchemes.MsEntra, AuthSchemes.Google })
                {
                    if (options.SchemeMap.TryGetValue(schemeName, out var scheme))
                        scheme.HandlerType = typeof(TestAuthHandler);
                }
            });
        });
    }

    /// <summary>An HttpClient already carrying a bootstrap bearer obtained through the real login flow.</summary>
    public async ValueTask<HttpClient> ECommerceClientAsync(EUserType user)
    {
        var client = CreateClient();
        if (!_accessTokenCache.TryGetValue(user, out var accessToken))
        {
            accessToken = await LoginAsync(user);
            _accessTokenCache[user] = accessToken;
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public void ClearAccessTokenCache() => _accessTokenCache.Clear();

    private async ValueTask<string> LoginAsync(EUserType user)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, user.ToString());

        var response = await client.PostAsync("/api/v1/auth/ms-entra", content: null, TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException(
                $"Login failed ({response.StatusCode}): {problem}\nServer errors:\n{string.Join('\n', ServerErrors)}");
        }

        var body = await response.Content.ReadFromJsonAsync<RequestTokenResponse>(TestContext.Current.CancellationToken);
        return body?.AccessToken ?? throw new InvalidOperationException("Login returned no access token.");
    }
}
