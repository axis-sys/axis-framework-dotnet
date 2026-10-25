using System.Text.Json;
using System.Text.Json.Nodes;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Http;

internal static class ProblemResponses
{
    private const string TraceId = "traceId";

    public static async Task<string[]> ErrorCodesAsync(HttpResponseMessage response)
    {
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return [.. problem.RootElement.GetProperty("errors").EnumerateArray().Select(error => error.GetProperty("code").GetString() ?? string.Empty)];
    }

    public static async Task<string> WithoutTraceIdAsync(HttpResponseMessage response)
    {
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        problem.Remove(TraceId);
        return problem.ToJsonString();
    }
}
