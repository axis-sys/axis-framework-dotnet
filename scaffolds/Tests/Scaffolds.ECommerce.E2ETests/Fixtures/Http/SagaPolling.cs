namespace Scaffolds.ECommerce.E2ETests.Fixtures.Http;

internal static class SagaPolling
{
    public static async Task<T> PollUntilAsync<T>(
        HttpClient client,
        string runUrl,
        Func<T, bool> reached,
        IEnumerable<string> serverLogs,
        CancellationToken cancellationToken,
        int maxAttempts = 300,
        int delayMilliseconds = 100)
        where T : class
    {
        T? last = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await client.GetAsync(runUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
                continue;
            }

            await HttpAssert.StatusAsync(HttpStatusCode.OK, response, serverLogs);
            last = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            Assert.NotNull(last);

            if (reached(last))
                return last;

            await Task.Delay(delayMilliseconds, cancellationToken);
        }

        throw new TimeoutException(
            $"Run at '{runUrl}' never reached the expected state (last: {last?.ToString() ?? "<none>"}).\nServer logs:\n{string.Join('\n', serverLogs)}");
    }
}
