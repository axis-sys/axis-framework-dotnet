using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Scaffolds.ECommerce.E2ETests.Fixtures.Factories;

// Replaces the default logging providers (testing-e2e-quiet-logging-providers) and collects
// Warning+ entries so a test can assert the server stayed quiet.
public sealed class CollectingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CollectingLogger(categoryName, sink);

    public void Dispose()
    {
    }

    private sealed class CollectingLogger(string category, ConcurrentQueue<string> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                sink.Enqueue($"[{logLevel}] {category}: {formatter(state, exception)}");
        }
    }
}
