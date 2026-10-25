using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;
using Scaffolds.ECommerce.Host.Authorization;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Authorization;

public static class UpdateGrantsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ReplacesTheGrantsWhenAnAdministratorUpdatesThemAsync(ECommerceWebAppFactory factory)
    {
        var administrator = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var (_, customerId, _) = await factory.NewCustomerClientAsync();

        var response = await AuthorizationSteps.UpdateGrantsAsync(administrator, customerId, disabled: false, PermissionCodes.CatalogRegisterProducts);

        await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
        var grants = await response.Content.ReadFromJsonAsync<UpdateGrantsResponse>(Ct);
        Assert.NotNull(grants);
        Assert.Equal(customerId, grants.CustomerId);
        Assert.False(grants.Disabled);
        Assert.Equal([PermissionCodes.CatalogRegisterProducts], grants.Permissions);
    }

    public static async Task ReturnsForbiddenWhenTheCallerIsNotAnAdministratorAsync(ECommerceWebAppFactory factory)
    {
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);
        var (_, customerId, _) = await factory.NewCustomerClientAsync();

        var response = await AuthorizationSteps.UpdateGrantsAsync(staff, customerId, disabled: false, PermissionCodes.CatalogRegisterProducts);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal([AuthorizationErrors.AdministratorRequired.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await AuthorizationSteps.UpdateGrantsAsync(client, Guid.CreateVersion7().ToString(), disabled: false);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public static async Task ReturnsBadRequestWhenAPermissionIsUnknownAsync(ECommerceWebAppFactory factory)
    {
        var administrator = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var (_, customerId, _) = await factory.NewCustomerClientAsync();

        var response = await AuthorizationSteps.UpdateGrantsAsync(administrator, customerId, disabled: false, "catalog.delete-everything");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([MemberErrors.PermissionUnknown], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsNotFoundWhenTheCustomerIsUnknownAsync(ECommerceWebAppFactory factory)
    {
        var administrator = await factory.ECommerceClientAsync(EUserType.SystemAdmin);

        var response = await AuthorizationSteps.UpdateGrantsAsync(administrator, Guid.CreateVersion7().ToString(), disabled: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([CustomerErrors.CustomerNotFound.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task AuthorizesTheNextRequestsOfTheMemberWhenAnAdministratorGrantsThePermissionAsync(ECommerceWebAppFactory factory)
    {
        var administrator = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var (member, customerId, _) = await factory.NewCustomerClientAsync();
        var refused = await member.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);

        var granted = await AuthorizationSteps.UpdateGrantsAsync(administrator, customerId, disabled: false, PermissionCodes.CatalogRegisterProducts);
        var answered = await AuthorizationSteps.RegisterAProductUntilAnsweredAsync(member, HttpStatusCode.Created);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, granted.StatusCode);
        Assert.Equal(HttpStatusCode.Created, answered);
    }

    public static async Task RefusesTheNextRequestsOfTheMemberWhenAnAdministratorDisablesTheMembershipAsync(ECommerceWebAppFactory factory)
    {
        var administrator = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var (member, customerId, _) = await factory.NewCustomerClientAsync();
        Assert.Equal(HttpStatusCode.OK, (await AuthorizationSteps.UpdateGrantsAsync(administrator, customerId, disabled: false, PermissionCodes.CatalogRegisterProducts)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, await AuthorizationSteps.RegisterAProductUntilAnsweredAsync(member, HttpStatusCode.Created));

        var disabled = await AuthorizationSteps.UpdateGrantsAsync(administrator, customerId, disabled: true, PermissionCodes.CatalogRegisterProducts);
        var answered = await AuthorizationSteps.RegisterAProductUntilAnsweredAsync(member, HttpStatusCode.Forbidden);

        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, answered);
    }
}
