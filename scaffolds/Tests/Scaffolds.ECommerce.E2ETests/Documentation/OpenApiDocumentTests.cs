using System.Text.Json;
using Scaffolds.ECommerce.E2ETests.Fixtures.Factories;
using Scaffolds.ECommerce.E2ETests.Fixtures.Http;
using Scaffolds.ECommerce.Host.OpenApi;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.E2ETests.Documentation;

public static class OpenApiDocumentTests
{
    private static readonly (string Path, string Method, string[] Responses)[] Operations =
    [
        (Routes.Sessions, "post", ["200", "400", "401"]),
        (Routes.Products, "post", ["201", "400", "401", "403"]),
        ($"{Routes.Products}/{{productId}}", "get", ["200", "400", "401", "404"]),
        ($"{Routes.Products}/{{productId}}/checkout", "post", ["200", "400", "401", "404", "422"]),
        (Routes.Orders, "post", ["201", "400", "401", "404"]),
        ($"{Routes.Orders}/{{orderId}}", "get", ["200", "400", "401", "404"]),
        (Routes.EmailValidationCodes, "post", ["200", "401", "404"]),
        (Routes.EmailValidations, "post", ["202", "400", "401", "404"]),
        ($"{Routes.EmailValidations}/{{sagaId}}", "get", ["200", "400", "401", "404"]),
        ($"{Routes.EmailValidations}/{{sagaId}}/cancel", "post", ["200", "400", "401", "404", "409"]),
        (Routes.DatabaseMigrations, "post", ["200", "401", "403", "500", "503"]),
        ($"{Routes.Members}/{{customerId}}/grants", "put", ["200", "400", "401", "403", "404"]),
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task DocumentsEveryOperationWithItsResponsesWhenTheHostRunsInDevelopmentAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.OpenApiV1, Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var paths = document.RootElement.GetProperty("paths");
        Assert.Equal(Operations.Select(operation => operation.Path).Distinct().Order(), paths.EnumerateObject().Select(path => path.Name).Order());
        foreach (var (path, method, responses) in Operations)
        {
            var operation = paths.GetProperty(path).GetProperty(method);
            Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()), $"{method} {path} has no summary.");
            Assert.Equal(responses, operation.GetProperty("responses").EnumerateObject().Select(code => code.Name).Order());
        }
    }

    public static async Task DescribesTheApiAndItsSchemesWhenTheHostRunsInDevelopmentAsync(ECommerceWebAppFactory factory)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Routes.OpenApiV1, Ct);

        await HttpAssert.StatusAsync(HttpStatusCode.OK, response, factory.ServerErrors);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var root = document.RootElement;
        Assert.Equal(ECommerceOpenApi.Description, root.GetProperty("info").GetProperty("description").GetString());
        var schemes = root.GetProperty("components").GetProperty("securitySchemes").EnumerateObject().Select(scheme => scheme.Name);
        Assert.Equal([AuthSchemes.ECommerce, AuthSchemes.Google, AuthSchemes.MsEntra], schemes.Order());
        var sessionSecurity = root.GetProperty("paths").GetProperty(Routes.Sessions).GetProperty("post").GetProperty("security")
            .EnumerateArray().SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name));
        Assert.Equal([AuthSchemes.Google, AuthSchemes.MsEntra], sessionSecurity.Order());
        var accepted = root.GetProperty("paths").GetProperty(Routes.EmailValidations).GetProperty("post").GetProperty("responses").GetProperty("202");
        Assert.True(accepted.GetProperty("headers").TryGetProperty("Location", out _));
    }
}
