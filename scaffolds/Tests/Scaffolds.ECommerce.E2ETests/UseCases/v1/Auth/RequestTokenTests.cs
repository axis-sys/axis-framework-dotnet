using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth;

public static class RequestTokenTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task IssuesASessionTokenWhenTheMsEntraTokenIsValidAsync(ECommerceWebAppFactory factory)
        => IssuesASessionTokenAsync(factory, AuthSchemes.MsEntra);

    public static Task IssuesASessionTokenWhenTheGoogleTokenIsValidAsync(ECommerceWebAppFactory factory)
        => IssuesASessionTokenAsync(factory, AuthSchemes.Google);

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Routes.Sessions, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheExternalTokenHasNoUsableSubjectAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, EUserType.NoSubjectUser.ToString());

        var response = await client.PostAsync(Routes.Sessions, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerPresentsASessionTokenAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.PostAsync(Routes.Sessions, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task IssuesASessionTokenAsync(ECommerceWebAppFactory factory, string provider)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, EUserType.NormalUser.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.ProviderHeader, provider);

        var response = await client.PostAsync(Routes.Sessions, content: null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<RequestTokenResponse>(Ct);
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.CustomerId));
        Assert.True(session.AccessTokenExpiresAt > DateTimeOffset.UtcNow);
    }
}
