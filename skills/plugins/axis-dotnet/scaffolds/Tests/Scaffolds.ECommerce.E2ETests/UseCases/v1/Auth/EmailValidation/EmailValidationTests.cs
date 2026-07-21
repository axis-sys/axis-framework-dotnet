using Microsoft.Extensions.DependencyInjection;
using Scaffolds.ECommerce.Adapters.Driven.InMemory.Auth;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;
using Scaffolds.ECommerce.E2ETests.Fixtures.Auth;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.Auth.EmailValidation;

/// <summary>
/// The email-validation round trip: request a code by email, echo it back, and poll the accepted saga
/// run (202 + status polling) until the cross-BC mark and code removal both commit.
/// </summary>
public sealed class EmailValidationTests
{
    private const string RequestEndpoint = "/api/v1/auth/email-validation/request";
    private const string ValidateEndpoint = "/api/v1/auth/email-validation/validate";
    private static readonly string[] Terminal = ["Completed", "Compensated", "Failed"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Happy path tests
    public static async Task RoundTripValidatesTheEmailAsync(ECommerceWebAppFactory factory)
    {
        var client = await factory.ECommerceClientAsync(EUserType.NormalUser);

        var request = await client.PostAsync(RequestEndpoint, content: null, Ct);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var requested = await request.Content.ReadFromJsonAsync<RequestEmailValidationResponse>(Ct);
        Assert.NotNull(requested);
        Assert.Equal(TestUsers.Email(EUserType.NormalUser), requested.SentTo);

        // The in-memory outbox stands in for the SMTP adapter; the code travels only through the email body.
        var outbox = factory.Services.GetRequiredService<InMemoryEmailOutbox>();
        var email = Assert.Single(outbox.Sent.Where(mail => mail.To.Any(to => to.Email == requested.SentTo)).TakeLast(1));
        var code = email.Body.Split(' ').Last().TrimEnd('.');

        var validate = await client.PostAsJsonAsync(ValidateEndpoint, new ValidateEmailCommand { Code = code }, Ct);
        Assert.Equal(HttpStatusCode.Accepted, validate.StatusCode);
        var accepted = await validate.Content.ReadFromJsonAsync<ValidateEmailResponse>(Ct);
        Assert.NotNull(accepted);
        Assert.False(string.IsNullOrWhiteSpace(accepted.SagaId));

        var status = await SagaPolling.PollUntilTerminalAsync<GetValidationStatusResponse>(
            client, $"{ValidateEndpoint}/{accepted.SagaId}", s => s.Status, Terminal, Ct);

        Assert.True(status.Status == "Completed", $"Status={status.Status} ErrorCode={status.ErrorCode} ErrorMessage={status.ErrorMessage}");
        Assert.True(status.EmailValidated);
    }

    // Negative path tests
    public static async Task MismatchedCodeReturns400Async(ECommerceWebAppFactory factory)
    {
        var client = await factory.ECommerceClientAsync(EUserType.SystemAdmin);

        var request = await client.PostAsync(RequestEndpoint, content: null, Ct);
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);

        var validate = await client.PostAsJsonAsync(ValidateEndpoint, new ValidateEmailCommand { Code = "wrong-code" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, validate.StatusCode);
    }

    public static async Task AnonymousRequestReturns401Async(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(RequestEndpoint, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
