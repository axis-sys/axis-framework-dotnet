using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace AxisLogger.UnitTests;

public class AxisResultLoggingExtensionsTests
{
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope)> Entries { get; } = new();
        private IReadOnlyDictionary<string, object?>? _currentScope;

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            _currentScope = state as IReadOnlyDictionary<string, object?>
                            ?? ((IEnumerable<KeyValuePair<string, object?>>)state).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            return new ScopeHandle(() => _currentScope = null);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception), _currentScope ?? new Dictionary<string, object?>()));

        private sealed class ScopeHandle(Action onDispose) : IDisposable
        {
            public void Dispose() => onDispose();
        }
    }

    private sealed record TestRequest;

    private static (IAxisLogger<TestRequest> Logger, CapturingLogger<TestRequest> Captured) CreateLogger()
    {
        var mediator = new Mock<IAxisMediator>();
        var captured = new CapturingLogger<TestRequest>();

        var services = new ServiceCollection();
        services.AddSingleton(mediator.Object);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ILogger<TestRequest>>(captured);
        services.AddAxisLogger();

        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IAxisLogger<TestRequest>>(), captured);
    }

    // ── LogIfFailure (AxisResult) ───────────────────────────────────────────

    [Fact]
    public void LogIfFailure_OnSuccess_DoesNotLogAndReturnsSameResult()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Ok();

        var returned = result.LogIfFailure(logger, AxisFailureLogSeverity.Warning, "should not log");

        Assert.Empty(captured.Entries);
        Assert.Same(result, returned);
    }

    [Fact]
    public void LogIfFailure_OnFailure_LogsAtWarningByDefault()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error(AxisError.BusinessRule("BOOM"));

        result.LogIfFailure(logger, AxisFailureLogSeverity.Warning, "op failed", ("Pass", "reconciler"));

        var entry = Assert.Single(captured.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("op failed", entry.Message);
        Assert.Equal("reconciler", entry.Scope["Pass"]);
        Assert.Same(result.Errors, entry.Scope["AxisErrorList"]);
    }

    [Fact]
    public void LogIfFailure_OnFailure_LogsAtErrorWhenRequested()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error(AxisError.InternalServerError("BOOM"));

        result.LogIfFailure(logger, AxisFailureLogSeverity.Error, "op failed hard");

        Assert.Equal(LogLevel.Error, captured.Entries.Single().Level);
    }

    [Fact]
    public void LogIfFailure_ReturnsSameFailureForChaining()
    {
        var (logger, _) = CreateLogger();
        var result = AxisResult.Error(AxisError.BusinessRule("BOOM"));

        var returned = result.LogIfFailure(logger, AxisFailureLogSeverity.Warning, "x");

        Assert.Same(result, returned);
    }

    // ── LogIfFailure (AxisResult<T>) ────────────────────────────────────────

    [Fact]
    public void LogIfFailure_Generic_OnFailure_LogsAndPreservesGenericType()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error<int>(AxisError.NotFound("MISSING"));

        var returned = result.LogIfFailure(logger, AxisFailureLogSeverity.Warning, "not found");

        Assert.Same(result, returned);
        var entry = Assert.Single(captured.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void LogIfFailure_Generic_OnSuccess_DoesNotLogAndValueSurvives()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Ok(42);

        var returned = result.LogIfFailure(logger, AxisFailureLogSeverity.Warning, "x");

        Assert.Empty(captured.Entries);
        Assert.Equal(42, returned.Value);
    }

    // ── LogIfSuccess (AxisResult) ────────────────────────────────────────────

    [Fact]
    public void LogIfSuccess_OnFailure_DoesNotLogAndReturnsSameResult()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error(AxisError.BusinessRule("BOOM"));

        var returned = result.LogIfSuccess(logger, AxisSuccessLogSeverity.Information, "should not log");

        Assert.Empty(captured.Entries);
        Assert.Same(result, returned);
    }

    [Fact]
    public void LogIfSuccess_OnSuccess_LogsAtInformationByDefault()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Ok();

        result.LogIfSuccess(logger, AxisSuccessLogSeverity.Information, "op ok", ("Pass", "reconciler"));

        var entry = Assert.Single(captured.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("op ok", entry.Message);
        Assert.Equal("reconciler", entry.Scope["Pass"]);
    }

    [Fact]
    public void LogIfSuccess_OnSuccess_LogsAtWarningWhenRequested()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Ok();

        result.LogIfSuccess(logger, AxisSuccessLogSeverity.Warning, "resumed after backoff");

        Assert.Equal(LogLevel.Warning, captured.Entries.Single().Level);
    }

    // ── LogIfSuccess (AxisResult<T>) ─────────────────────────────────────────

    [Fact]
    public void LogIfSuccess_Generic_OnSuccess_LogsAndPreservesValue()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Ok(42);

        var returned = result.LogIfSuccess(logger, AxisSuccessLogSeverity.Information, "ok");

        Assert.Single(captured.Entries);
        Assert.Equal(42, returned.Value);
    }

    [Fact]
    public void LogIfSuccess_Generic_OnFailure_DoesNotLog()
    {
        var (logger, captured) = CreateLogger();
        var result = AxisResult.Error<int>(AxisError.NotFound("MISSING"));

        result.LogIfSuccess(logger, AxisSuccessLogSeverity.Information, "ok");

        Assert.Empty(captured.Entries);
    }
}
