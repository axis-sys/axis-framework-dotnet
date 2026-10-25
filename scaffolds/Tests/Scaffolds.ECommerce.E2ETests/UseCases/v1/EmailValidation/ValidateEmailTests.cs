using Axis.SharedKernel;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.EmailValidation;

public static class ValidateEmailTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task ValidatesTheEmailWhenTheCustomerEchoesTheEmailedCodeAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        var staff = await factory.ECommerceClientAsync(EUserType.Staff);
        var requested = await EmailValidationSteps.RequestCodeAsync(customer);
        var code = EmailValidationSteps.CodeEmailedTo(factory, requested.SentTo);

        var response = await customer.PostAsJsonAsync(Routes.EmailValidations, new ValidateEmailCommand { Code = code }, Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.Accepted, response, factory.ServerErrors);
        var accepted = await response.Content.ReadFromJsonAsync<ValidateEmailResponse>(Ct);
        Assert.NotNull(accepted);
        Assert.Equal(nameof(AxisSagaStatus.Pending), accepted.Status);
        Assert.Equal(Routes.EmailValidationRun(accepted.SagaId), response.Headers.Location?.OriginalString);
        Assert.NotNull(response.Headers.RetryAfter);
        var run = await EmailValidationSteps.PollUntilTerminalAsync(factory, staff, accepted.SagaId);
        Assert.True(run.Status == nameof(AxisSagaStatus.Completed), $"Status={run.Status} ErrorCode={run.ErrorCode} ErrorMessage={run.ErrorMessage}");
        Assert.True(run.EmailValidated);
    }

    public static async Task ReturnsBadRequestWhenTheCodeDoesNotMatchAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, _) = await factory.NewCustomerClientAsync();
        await EmailValidationSteps.RequestCodeAsync(customer);

        var response = await customer.PostAsJsonAsync(Routes.EmailValidations, new ValidateEmailCommand { Code = "wrong-code" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Routes.EmailValidations, new ValidateEmailCommand { Code = "any-code" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
