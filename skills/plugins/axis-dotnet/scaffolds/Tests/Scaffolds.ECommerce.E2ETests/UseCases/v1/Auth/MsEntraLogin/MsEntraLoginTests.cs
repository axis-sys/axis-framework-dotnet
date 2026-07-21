using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.MsEntraLogin;

/// <summary>Phase-one sign-in via Microsoft Entra: <c>POST /api/v1/auth/ms-entra</c>.</summary>
public sealed class MsEntraLoginTests
{
    private const string Endpoint = "/api/v1/auth/ms-entra";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Happy path tests
    public static async Task ExchangesEntraTokenForBootstrapTokenAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, EUserType.NormalUser.ToString());

        var response = await client.PostAsync(Endpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RequestTokenResponse>(Ct);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.CustomerId));
        Assert.True(body.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
    }

    // Negative path tests
    public static async Task AnonymousReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Endpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task TokenWithoutUsableSubjectReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, EUserType.NoSubjectUser.ToString());

        var response = await client.PostAsync(Endpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
