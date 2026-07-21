namespace Axis;

/// <summary>Severity for <see cref="AxisResultLoggingExtensions.LogIfFailure{T}"/>.</summary>
public enum AxisFailureLogSeverity { Warning, Error }

/// <summary>Severity for <see cref="AxisResultLoggingExtensions.LogIfSuccess{T}"/>.</summary>
public enum AxisSuccessLogSeverity { Information, Warning }

/// <summary>
/// Tap-family side-effect logging for AxisResult: log the outcome and return the SAME result unchanged,
/// so the call slots into a Then/Match chain instead of requiring a manual
/// <c>if (result.IsFailure) logger.LogWarning(...)</c> check at every call site. Mirrors Tap/TapError's
/// shape (side effect on one rail, passthrough on the other); the error list is auto-appended as the
/// "AxisErrorList" property, the same key <see cref="IAxisLogger{T}.LogResult"/> already uses.
/// </summary>
public static class AxisResultLoggingExtensions
{
    public static AxisResult LogIfFailure<T>(
        this AxisResult result, IAxisLogger<T> logger, AxisFailureLogSeverity severity, string message,
        params (string Key, object? Value)[] properties)
        => result.TapError(errors => Write(logger, severity, message, Enrich(errors, properties)));

    public static AxisResult<TValue> LogIfFailure<T, TValue>(
        this AxisResult<TValue> result, IAxisLogger<T> logger, AxisFailureLogSeverity severity, string message,
        params (string Key, object? Value)[] properties)
        => result.TapError(errors => Write(logger, severity, message, Enrich(errors, properties)));

    public static AxisResult LogIfSuccess<T>(
        this AxisResult result, IAxisLogger<T> logger, AxisSuccessLogSeverity severity, string message,
        params (string Key, object? Value)[] properties)
        => result.Tap(() => Write(logger, severity, message, properties));

    public static AxisResult<TValue> LogIfSuccess<T, TValue>(
        this AxisResult<TValue> result, IAxisLogger<T> logger, AxisSuccessLogSeverity severity, string message,
        params (string Key, object? Value)[] properties)
        => result.Tap(() => Write(logger, severity, message, properties));

    private static (string Key, object? Value)[] Enrich(IReadOnlyList<AxisError> errors, (string Key, object? Value)[] properties)
        => [.. properties, ("AxisErrorList", errors)];

    private static void Write<T>(IAxisLogger<T> logger, AxisFailureLogSeverity severity, string message, (string, object?)[] properties)
    {
        switch (severity)
        {
            case AxisFailureLogSeverity.Error: logger.LogError(message, properties); break;
            default: logger.LogWarning(message, properties); break;
        }
    }

    private static void Write<T>(IAxisLogger<T> logger, AxisSuccessLogSeverity severity, string message, (string, object?)[] properties)
    {
        switch (severity)
        {
            case AxisSuccessLogSeverity.Warning: logger.LogWarning(message, properties); break;
            default: logger.LogInformation(message, properties); break;
        }
    }
}
