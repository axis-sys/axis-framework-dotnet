using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

namespace Scaffolds.ECommerce.E2ETests.UseCases.v1.EmailValidation;

public static class RequestEmailValidationTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task EmailsACodeToTheAddressOnRecordWhenTheCustomerAsksAsync(ECommerceWebAppFactory factory)
    {
        var (customer, _, email) = await factory.NewCustomerClientAsync();

        var requested = await EmailValidationSteps.RequestCodeAsync(customer);

        Assert.Equal(email, requested.SentTo);
        Assert.False(string.IsNullOrWhiteSpace(EmailValidationSteps.CodeEmailedTo(factory, requested.SentTo)));
    }

    public static async Task ReturnsUnauthorizedWhenTheCallerIsAnonymousAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync(Routes.EmailValidationCodes, content: null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
