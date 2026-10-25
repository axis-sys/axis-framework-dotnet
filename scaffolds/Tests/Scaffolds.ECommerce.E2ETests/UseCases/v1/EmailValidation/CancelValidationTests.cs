using Axis.SharedKernel;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.EmailValidation;

public static class CancelValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task CancelsTheRunGracefullyWhenItIsStillRunningAsync(ECommerceWebAppFactory factory)
    {
        var (customer, customerId, _) = await factory.NewCustomerClientAsync();
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);
        string sagaId;
        await using (await factory.LockCustomerAsync(customerId))
        {
            sagaId = (await EmailValidationSteps.StartValidationAsync(factory, customer)).SagaId;
            await SagaPolling.PollUntilAsync<GetValidationStatusResponse>(
                staff, Routes.EmailValidationRun(sagaId), run => run.Status == nameof(AxisSagaStatus.Running), factory.ServerErrors, Ct);

            var response = await staff.PostAsync(Routes.EmailValidationRunCancel(sagaId), content: null, Ct);

            await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
            var cancelling = await response.Content.ReadFromJsonAsync<CancelValidationResponse>(Ct);
            Assert.NotNull(cancelling);
            Assert.Equal(nameof(AxisSagaStatus.Cancelling), cancelling.Status);
        }

        var run = await EmailValidationSteps.PollUntilTerminalAsync(factory, staff, sagaId);

        Assert.Equal(nameof(AxisSagaStatus.Cancelled), run.Status);
    }

    public static async Task ReturnsConflictWhenTheRunAlreadyCompletedAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);
        var accepted = await EmailValidationSteps.StartValidationAsync(factory, customer);
        Assert.Equal(nameof(AxisSagaStatus.Completed), (await EmailValidationSteps.PollUntilTerminalAsync(factory, staff, accepted.SagaId)).Status);

        var response = await staff.PostAsync(Routes.EmailValidationRunCancel(accepted.SagaId), content: null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal([SharedErrors.SagaRunNotCancellable.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsTheUnknownRunAnswerWhenTheCallerLacksTheSagaPermissionAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var accepted = await EmailValidationSteps.StartValidationAsync(factory, customer);

        var response = await customer.PostAsync(Routes.EmailValidationRunCancel(accepted.SagaId), content: null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal([SharedErrors.SagaRunNotFound.Code], await ProblemResponses.ErrorCodesAsync(response));
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Routes.EmailValidationRunCancel(Guid.CreateVersion7().ToString()), content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
