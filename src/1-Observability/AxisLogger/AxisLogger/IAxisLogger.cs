using AxisMediator.Contracts;

namespace Axis;

/// <summary>
/// Request-scoped structured logger. Automatically enriches entries with the
/// ambient trace/journey/origin identifiers resolved from <see cref="IAxisMediator"/>.
/// </summary>
public interface IAxisLogger<in T>
{
    void LogDebug(string message, params (string Key, object? Value)[] properties);
    void LogInformation(string message, params (string Key, object? Value)[] properties);
    void LogWarning(string message, params (string Key, object? Value)[] properties);
    void LogError(string message, params (string Key, object? Value)[] properties);
    void LogError(Exception exception, string message, params (string Key, object? Value)[] properties);
    void LogCritical(string message, params (string Key, object? Value)[] properties);

    /// <summary>Structured logging for an <see cref="AxisResult"/> outcome.</summary>
    void LogResult(string tag, AxisResult result);
}
