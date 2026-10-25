using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.EmailValidation;

public static class GetValidationStatusTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ReadsTheRunWhenTheCallerIsAnAdministratorAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var admin = await factory.ECommerceClientAsync(EUserType.SystemAdmin);
        var accepted = await EmailValidationSteps.StartValidationAsync(factory, customer);

        var response = await admin.GetAsync(Routes.EmailValidationRun(accepted.SagaId), Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
        var run = await response.Content.ReadFromJsonAsync<GetValidationStatusResponse>(Ct);
        Assert.NotNull(run);
        Assert.Equal(accepted.SagaId, run.SagaId);
    }

    public static async Task ReturnsTheUnknownRunAnswerWhenTheCallerLacksTheSagaPermissionAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);
        var accepted = await EmailValidationSteps.StartValidationAsync(factory, customer);
        var unknown = await staff.GetAsync(Routes.EmailValidationRun(Guid.CreateVersion7().ToString()), Ct);

        var concealed = await customer.GetAsync(Routes.EmailValidationRun(accepted.SagaId), Ct);

        Assert.Equal(HttpStatusCode.NotFound, concealed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal([SharedErrors.SagaRunNotFound.Code], await ProblemResponses.ErrorCodesAsync(concealed));
        Assert.Equal(await ProblemResponses.WithoutTraceIdAsync(unknown), await ProblemResponses.WithoutTraceIdAsync(concealed));
    }

    public static async Task ReturnsNotFoundWhenTheRunIsUnknownAsync(ECommerceWebAppFactory factory)
    {
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);

        var response = await staff.GetAsync(Routes.EmailValidationRun(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([SharedErrors.SagaRunNotFound.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.EmailValidationRun(Guid.CreateVersion7().ToString()), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
