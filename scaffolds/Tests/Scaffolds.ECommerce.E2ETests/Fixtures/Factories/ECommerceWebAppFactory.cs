using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driven.Admin;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.Host.Persistence;
using Scaffolds.ECommerce.SharedKernel.Auth;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

public abstract class ECommerceWebAppFactory : WebApplicationFactory<Host.Program>, IAsyncLifetime
{
    protected const int MaxPoolSizeWithinContainerConnectionLimit = 20;
    private const string MvcTestingAppManifest = "MvcTestingAppManifest.json";
    private const string DotNet = "dotnet";
    private const string DotNetHostPath = "DOTNET_HOST_PATH";
    private const string DotNetEnvironment = "DOTNET_ENVIRONMENT";
    private const string AspNetCoreEnvironment = "ASPNETCORE_ENVIRONMENT";

    public ConcurrentQueue<string> ServerErrors { get; } = new();

    private readonly Dictionary<EUserType, string> _accessTokenCache = [];
    private string _connectionString = string.Empty;

    protected abstract string ProviderName { get; }
    protected abstract string ConnectionStringName { get; }
    protected abstract Task<string> StartContainerAsync();
    protected abstract Task MigrateFrameworkStoresAsync(string connectionString);
    protected abstract Task StopContainerAsync();
    protected abstract DbConnection NewConnection(string connectionString);
    protected abstract DbConnectionStringBuilder NewConnectionStringBuilder(string connectionString);

    public abstract Task<EmptyServer> StartEmptyServerAsync();

    public async ValueTask InitializeAsync()
    {
        var connectionString = await StartContainerAsync();
        _connectionString = connectionString;
        foreach (var (variable, value) in BootEnvironment(connectionString))
            Environment.SetEnvironmentVariable(variable, value);

        await MigrateFrameworkStoresAsync(connectionString);
        using var warmup = CreateClient();
        await MigrateThroughThePortAsync();
        await GrantTheStaffMembersAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        foreach (var (variable, _) in BootEnvironment(connectionString: string.Empty))
            Environment.SetEnvironmentVariable(variable, null);

        await StopContainerAsync();
        await base.DisposeAsync();
    }

    public string ConnectionStringWithPassword(string password)
    {
        var builder = NewConnectionStringBuilder(_connectionString);
        builder["Password"] = password;
        return builder.ConnectionString;
    }

    public async Task<HostCommandRun> RunHostCommandAsync(string connectionString, params string[] command)
    {
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable(DotNetHostPath) ?? DotNet)
        {
            WorkingDirectory = HostContentRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add(typeof(Host.Program).Assembly.Location);
        foreach (var argument in command)
            start.ArgumentList.Add(argument);
        foreach (var (variable, value) in BootEnvironment(connectionString))
            start.Environment[variable] = value;
        start.Environment[DotNetEnvironment] = Environments.Production;
        start.Environment[AspNetCoreEnvironment] = Environments.Production;

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        var errors = process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);
        return new HostCommandRun(process.ExitCode, await output + await errors);
    }

    private (string Variable, string Value)[] BootEnvironment(string connectionString) =>
    [
        ($"ConnectionStrings__{ConnectionStringName}", connectionString),
        (EnvironmentVariable(PersistenceKeys.DatabaseProvider), ProviderName),
        (EnvironmentVariable(PersistenceKeys.SagaProvider), ProviderName),
    ];

    private static string HostContentRoot()
    {
        var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, MvcTestingAppManifest)));
        return manifest![typeof(Host.Program).Assembly.FullName!];
    }

    private static string EnvironmentVariable(string configurationKey) => configurationKey.Replace(":", "__");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders().AddProvider(new CollectingLoggerProvider(ServerErrors)));

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

    public async ValueTask<HttpClient> ECommerceClientAsync(EUserType user)
    {
        if (!_accessTokenCache.TryGetValue(user, out var accessToken))
        {
            accessToken = (await LoginAsync(TestAuthHandler.UserHeader, user.ToString())).AccessToken;
            _accessTokenCache[user] = accessToken;
        }

        return BearerClient(accessToken);
    }

    public async ValueTask<(HttpClient Client, string CustomerId, string Email)> NewCustomerClientAsync()
    {
        var subject = Guid.CreateVersion7().ToString();
        var session = await LoginAsync(TestAuthHandler.SubjectHeader, subject);

        return (BearerClient(session.AccessToken), session.CustomerId, TestUsers.NewCustomerEmail(subject));
    }

    public Task<RowLock> LockCustomerAsync(string customerId)
        => RowLock.AcquireAsync(
            NewConnection(_connectionString),
            $"SELECT {CustomersColumns.CustomerId} FROM {CustomersTable.Table} WHERE {CustomersColumns.CustomerId} = @customerId FOR UPDATE",
            ("customerId", customerId));

    private HttpClient BearerClient(string accessToken)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private async Task MigrateThroughThePortAsync()
    {
        using var scope = Services.CreateScope();
        var databaseMigrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigratorPort>();

        (await databaseMigrator.MigrateAsync()).ShouldSucceed();
    }

    private async Task GrantTheStaffMembersAsync()
    {
        using var administrator = await ECommerceClientAsync(EUserType.SystemAdmin);
        await GrantAsync(administrator, EUserType.Staff, disabled: false, PermissionCodes.CatalogRegisterProducts, PermissionCodes.ValidateEmailRuns);
        await GrantAsync(administrator, EUserType.DisabledStaff, disabled: true, PermissionCodes.CatalogRegisterProducts);
    }

    private async Task GrantAsync(HttpClient administrator, EUserType member, bool disabled, params string[] permissions)
    {
        var session = await LoginAsync(TestAuthHandler.UserHeader, member.ToString());
        _accessTokenCache[member] = session.AccessToken;

        var response = await administrator.PutAsJsonAsync(
            Routes.MemberGrants(session.CustomerId),
            new UpdateGrantsCommand { Disabled = disabled, Permissions = permissions },
            TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException(
                $"Granting {member} failed ({response.StatusCode}): {problem}\nServer errors:\n{string.Join('\n', ServerErrors)}");
        }
    }

    private async ValueTask<RequestTokenResponse> LoginAsync(string identityHeader, string identity)
    {
        using var client = CreateClient();
        client.DefaultRequestHeaders.Add(identityHeader, identity);

        var response = await client.PostAsync(Routes.Sessions, content: null, TestContext.Current.CancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new InvalidOperationException(
                $"Login failed ({response.StatusCode}): {problem}\nServer errors:\n{string.Join('\n', ServerErrors)}");
        }

        return await response.Content.ReadFromJsonAsync<RequestTokenResponse>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("Login returned no session.");
    }
}
