using System.Diagnostics.CodeAnalysis;
using AxisMediator.Contracts;
using Microsoft.Extensions.Logging;

namespace Axis;

[SuppressMessage("Performance", "CA1873:Avoid logs than can be expensive")]
internal class AxisLogger<T>(IAxisMediator mediator, TimeProvider timeProvider, ILogger<T> logger) : IAxisLogger<T>
{
    private string UtcTime => timeProvider.GetUtcNow().ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

    public void LogDebug(string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Debug, null, message, properties);

    public void LogInformation(string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Information, null, message, properties);

    public void LogWarning(string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Warning, null, message, properties);

    public void LogError(string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Error, null, message, properties);

    public void LogError(Exception exception, string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Error, exception, message, properties);

    public void LogCritical(string message, params (string Key, object? Value)[] properties)
        => Write(LogLevel.Critical, null, message, properties);

    public void LogResult(string tag, AxisResult result)
    {
        var level = result.IsSuccess ? LogLevel.Information : LogLevel.Error;

        var extraProps = result.IsSuccess
            ? [("Tag", tag), ("RequestName", typeof(T).FullName)]
            : new[] { ("Tag", (object)tag), ("RequestName", typeof(T).FullName), ("AxisErrorList", result.Errors) };

        Write(level, null, $"{tag} Handled {typeof(T).Name}", extraProps);
    }

    private void Write(LogLevel level, Exception? exception, string message, (string Key, object? Value)[] properties)
    {
        if (!logger.IsEnabled(level)) return;

        using var scope = logger.BeginScope(BuildScope(properties));
        if (exception is null)
            logger.Log(level, message);
        else
            logger.Log(level, exception, message);
    }

    private Dictionary<string, object?> BuildScope((string Key, object? Value)[] properties)
    {
        var scope = new Dictionary<string, object?>(capacity: properties.Length + 4)
        {
            ["UtcTime"] = UtcTime,
            ["OriginId"] = mediator.OriginId,
            ["TraceId"] = mediator.TraceId,
            ["JourneyId"] = mediator.JourneyId,
        };
        foreach (var (key, value) in properties)
            scope[key] = value;
        return scope;
    }
}
