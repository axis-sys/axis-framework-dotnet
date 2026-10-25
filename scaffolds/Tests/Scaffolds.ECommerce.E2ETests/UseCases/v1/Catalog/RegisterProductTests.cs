using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;
using Scaffolds.ECommerce.Host.Authorization;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Catalog;

public static class RegisterProductTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task RegistersTheProductWhenTheMemberHoldsThePermissionAsync(ECommerceWebAppFactory factory)
    {
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);

        var productId = await CatalogSteps.RegisterProductAsync(staff, CatalogSteps.NewProduct());

        Assert.True(Guid.TryParse(productId, out _));
    }

    public static async Task RegistersTheProductWhenTheCallerIsAnAdministratorAsync(ECommerceWebAppFactory factory)
    {
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);

        var productId = await CatalogSteps.RegisterProductAsync(admin, CatalogSteps.NewProduct());

        Assert.True(Guid.TryParse(productId, out _));
    }

    public static async Task ReturnsForbiddenWhenTheCallerLacksThePermissionAsync(ECommerceWebAppFactory factory)
    {
        var customer = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var response = await customer.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([AuthorizationErrors.PermissionDenied.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsForbiddenWhenTheMemberIsDisabledAsync(ECommerceWebAppFactory factory)
    {
        var disabledStaff = await factory.ECommerceClientAsync(EUserType.DisabledStaff);

        var response = await disabledStaff.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([AuthorizationErrors.PermissionDenied.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
