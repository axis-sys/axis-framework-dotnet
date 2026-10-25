namespace Scaffolds.ECommerce.E2ETests.Fixtures.Http;

internal static class HttpAssert
{
    public static async Task StatusAsync(HttpStatusCode expected, HttpResponseMessage response, IEnumerable<string> serverLogs)
    {
        if (response.StatusCode == expected)
            return;

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Fail(
            $"Expected {(int)expected} {expected}, got {(int)response.StatusCode} {response.StatusCode}.\nBody: {body}\nServer logs:\n{string.Join('\n', serverLogs)}");
    }
}
