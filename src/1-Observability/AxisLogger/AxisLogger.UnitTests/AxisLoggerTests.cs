using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AxisLogger.UnitTests;

public class AxisLoggerTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> Scope)> Entries { get; } = new();
        public LogLevel EnabledLevel { get; set; } = LogLevel.Trace;
        private IReadOnlyDictionary<string, object?>? _currentScope;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _currentScope = state as IReadOnlyDictionary<string, object?>
                            ?? ((IEnumerable<KeyValuePair<string, object?>>)state)
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new ScopeHandle(() => _currentScope = null);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= EnabledLevel;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Entries.Add((logLevel, formatter(state, exception), exception, _currentScope ?? new Dictionary<string, object?>()));
        }

        private sealed class ScopeHandle(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }

    private sealed record TestRequest;

    private static (IAxisLogger<TestRequest> Logger, CapturingLogger<TestRequest> Captured) CreateLogger(LogLevel? minLevel = null)
    {
        var mediator = new Mock<IAxisMediator>();
        mediator.SetupGet(x => x.TraceId).Returns("trace-123");
        mediator.SetupGet(x => x.OriginId).Returns("origin-456");
        mediator.SetupGet(x => x.JourneyId).Returns("journey-789");

        var captured = new CapturingLogger<TestRequest>();
        if (minLevel.HasValue) captured.EnabledLevel = minLevel.Value;

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILogger<TestRequest>>(captured);
        services.AddAxisLogger();

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAxisLogger<TestRequest>>(), captured);
    }

    [Fact]
    public void LogDebugWritesEntryWithEnrichedScope()
    {
        var (logger, captured) = CreateLogger();

        logger.LogDebug("debug message", ("Key1", "Value1"));

        var entry = Assert.Single(captured.Entries);
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Equal("debug message", entry.Message);
        Assert.Null(entry.Exception);
        Assert.Equal("trace-123", entry.Scope["TraceId"]);
        Assert.Equal("origin-456", entry.Scope["OriginId"]);
        Assert.Equal("journey-789", entry.Scope["JourneyId"]);
        Assert.Equal("Value1", entry.Scope["Key1"]);
        Assert.NotNull(entry.Scope["UtcTime"]);
    }

    [Fact]
    public void LogInformationWritesAtInformationLevel()
    {
        var (logger, captured) = CreateLogger();

        logger.LogInformation("info message");

        Assert.Equal(LogLevel.Information, captured.Entries.Single().Level);
    }

    [Fact]
    public void LogWarningWritesAtWarningLevel()
    {
        var (logger, captured) = CreateLogger();

        logger.LogWarning("warn message");

        Assert.Equal(LogLevel.Warning, captured.Entries.Single().Level);
    }

    [Fact]
    public void LogErrorWithoutExceptionWritesErrorLevel()
    {
        var (logger, captured) = CreateLogger();

        logger.LogError("error message");

        var entry = captured.Entries.Single();
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public void LogErrorWithExceptionIncludesExceptionInEntry()
    {
        var (logger, captured) = CreateLogger();
        var exception = new InvalidOperationException("boom");

        logger.LogError(exception, "error with exception");

        var entry = captured.Entries.Single();
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void LogCriticalWritesAtCriticalLevel()
    {
        var (logger, captured) = CreateLogger();

        logger.LogCritical("crit message");

        Assert.Equal(LogLevel.Critical, captured.Entries.Single().Level);
    }

    [Fact]
    public void LogResultOnSuccessUsesInformationLevel()
    {
        var (logger, captured) = CreateLogger();

        logger.LogResult("Handled", AxisResult.Ok());

        var entry = captured.Entries.Single();
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Handled", entry.Scope["Tag"]);
        Assert.Equal(typeof(TestRequest).FullName, entry.Scope["RequestName"]);
    }

    [Fact]
    public void LogResultOnFailureUsesErrorLevelAndIncludesErrors()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error(AxisError.BusinessRule("BOOM"));

        logger.LogResult("Failed", result);

        var entry = captured.Entries.Single();
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Failed", entry.Scope["Tag"]);
        Assert.NotNull(entry.Scope["AxisErrorList"]);
    }

    [Fact]
    public void LogDoesNothingWhenLevelDisabled()
    {
        var (logger, captured) = CreateLogger(minLevel: LogLevel.Error);

        logger.LogDebug("ignored");
        logger.LogInformation("ignored");

        Assert.Empty(captured.Entries);
    }

    [Fact]
    public void AddAxisLoggerRegistersLoggerAsScoped()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IAxisMediator>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddAxisLogger();

        using var scope = services.BuildServiceProvider().CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<IAxisLogger<TestRequest>>();

        Assert.NotNull(logger);
    }
}
