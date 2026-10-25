using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Journeys;

internal static class AuthorizationSteps
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> UpdateGrantsAsync(HttpClient administrator, string customerId, bool disabled, params string[] permissions)
        => administrator.PutAsJsonAsync(
            Routes.MemberGrants(customerId),
            new UpdateGrantsCommand { Disabled = disabled, Permissions = permissions },
            Ct);

    public static async Task<HttpStatusCode> RegisterAProductUntilAnsweredAsync(
        HttpClient member, HttpStatusCode awaited, int maxAttempts = 150, int delayMilliseconds = 100)
    {
        var answered = HttpStatusCode.NoContent;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await member.PostAsJsonAsync(Routes.Products, CatalogSteps.NewProduct(), Ct);
            answered = response.StatusCode;
            if (answered == awaited)
                return answered;

            await Task.Delay(delayMilliseconds, Ct);
        }

        return answered;
    }
}
