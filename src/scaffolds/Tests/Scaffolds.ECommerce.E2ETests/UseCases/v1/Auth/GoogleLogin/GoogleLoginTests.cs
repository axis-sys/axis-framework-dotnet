using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.GoogleLogin;

/// <summary>Phase-one sign-in via Google Accounts: <c>POST /api/v1/auth/google</c>.</summary>
public sealed class GoogleLoginTests
{
    private const string Endpoint = "/api/v1/auth/google";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Happy path tests
    public static async Task ExchangesGoogleTokenForBootstrapTokenAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, EUserType.NormalUser.ToString());

        var response = await client.PostAsync(Endpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RequestTokenResponse>(Ct);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
    }

    // Negative path tests
    public static async Task AnonymousReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Endpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
