namespace Scaffolds.ECommerce.E2ETests.Fixtures.Http;

// Bounded polling for 202-accepted saga runs (testing-e2e-saga-edge-202-polling): a 404 means the run
// is not visible yet; anything else must be a 200 whose status eventually turns terminal.
internal static class SagaPolling
{
    public static async Task<T> PollUntilTerminalAsync<T>(
        HttpClient client,
        string statusUrl,
        Func<T, string> statusSelector,
        IReadOnlyCollection<string> terminalStatuses,
        CancellationToken cancellationToken,
        int maxAttempts = 300,
        int delayMilliseconds = 100)
        where T : class
    {
        T? last = null;
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var response = await client.GetAsync(statusUrl, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                await Task.Delay(delayMilliseconds, cancellationToken);
                continue;
            }

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            last = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            Assert.NotNull(last);

            if (terminalStatuses.Contains(statusSelector(last)))
                return last;

            await Task.Delay(delayMilliseconds, cancellationToken);
        }

        throw new TimeoutException(
            $"Run at '{statusUrl}' never reached a terminal status (last: {(last is null ? "<none>" : statusSelector(last))}).");
    }
}
