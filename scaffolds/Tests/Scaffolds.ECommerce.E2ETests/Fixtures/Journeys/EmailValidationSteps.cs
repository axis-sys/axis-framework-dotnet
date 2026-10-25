using Axis.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Scaffolds.ECommerce.Adapters.Driven.InMemory.EmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

internal static class EmailValidationSteps
{
    private static readonly string[] TerminalStatuses =
        [nameof(AxisSagaStatus.Completed), nameof(AxisSagaStatus.Compensated), nameof(AxisSagaStatus.Failed), nameof(AxisSagaStatus.Cancelled)];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<RequestEmailValidationResponse> RequestCodeAsync(HttpClient customer)
    {
        var response = await customer.PostAsync(Routes.EmailValidationCodes, content: null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var requested = await response.Content.ReadFromJsonAsync<RequestEmailValidationResponse>(Ct);
        Assert.NotNull(requested);
        return requested;
    }

    public static string CodeEmailedTo(ECommerceWebAppFactory factory, string email)
    {
        var outbox = factory.Services.GetRequiredService<InMemoryEmailOutbox>();
        var mail = outbox.Sent.Last(sent => sent.To.Any(to => to.Email == email));
        return mail.Body.Split(' ').Last().TrimEnd('.');
    }

    public static async Task<ValidateEmailResponse> StartValidationAsync(ECommerceWebAppFactory factory, HttpClient customer)
    {
        var requested = await RequestCodeAsync(customer);
        var code = CodeEmailedTo(factory, requested.SentTo);

        var response = await customer.PostAsJsonAsync(Routes.EmailValidations, new ValidateEmailCommand { Code = code }, Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.Accepted, response, factory.ServerErrors);
        var accepted = await response.Content.ReadFromJsonAsync<ValidateEmailResponse>(Ct);
        Assert.NotNull(accepted);
        return accepted;
    }

    public static Task<GetValidationStatusResponse> PollUntilTerminalAsync(ECommerceWebAppFactory factory, HttpClient reader, string sagaId)
        => SagaPolling.PollUntilAsync<GetValidationStatusResponse>(
            reader, Routes.EmailValidationRun(sagaId), run => TerminalStatuses.Contains(run.Status), factory.ServerErrors, Ct);
}
