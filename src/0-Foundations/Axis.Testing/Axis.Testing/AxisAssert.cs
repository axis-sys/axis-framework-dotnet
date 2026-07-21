namespace Axis.Testing;

/// <summary>
/// Framework-agnostic core of the Axis assertions. It inspects an <see cref="AxisResult"/> through the
/// safe positional Deconstruct (so it is ROP-analyzer-clean) and produces either the extracted value or
/// a rich, IDE-friendly failure message. It never throws a test-framework exception itself — the
/// per-framework adapters (Axis.Testing.XUnit, Axis.Testing.MSTest, ...) turn a failed check into their
/// native assertion exception and delegate value comparisons to their native Assert, so failures render
/// exactly like that framework's own.
/// </summary>
public static class AxisAssert
{
    public static bool TrySucceed<T>(AxisResult<T> result, string? because, out T value, out string message)
    {
        var (isSuccess, extracted, errors) = result;
        if (isSuccess)
        {
            value = extracted!;
            message = string.Empty;
            return true;
        }

        value = default!;
        message = Compose("Expected the result to succeed, but it failed", because, errors);
        return false;
    }

    public static bool TrySucceed(AxisResult result, string? because, out string message)
    {
        var (isSuccess, errors) = result;
        if (isSuccess)
        {
            message = string.Empty;
            return true;
        }

        message = Compose("Expected the result to succeed, but it failed", because, errors);
        return false;
    }

    public static bool TryFail(AxisResult result, string? because, out IReadOnlyList<AxisError> errors, out string message)
    {
        var (isSuccess, extractedErrors) = result;
        errors = extractedErrors;
        if (!isSuccess)
        {
            message = string.Empty;
            return true;
        }

        message = Prefix("Expected the result to fail, but it succeeded", because);
        return false;
    }

    public static string MissingCodeMessage(string expectedCode, IReadOnlyList<AxisError> errors, string? because)
        => Prefix($"Expected a failure with code \"{expectedCode}\", but the codes were: {Join(errors, e => e.Code)}", because);

    public static string MissingTypeMessage(AxisErrorType expectedType, IReadOnlyList<AxisError> errors, string? because)
        => Prefix($"Expected a failure of type {expectedType}, but the types were: {Join(errors, e => e.Type.ToString())}", because);

    public static string NotNullMessage(string? because)
        => Prefix("Expected the value to be non-null, but it was null", because);

    private static string Compose(string headline, string? because, IReadOnlyList<AxisError> errors)
        => Prefix(headline, because) + Environment.NewLine +
           (errors.Count == 0
               ? "  (no errors reported)"
               : string.Join(Environment.NewLine, errors.Select(e => $"  - [{e.Type}] {e.Code}")));

    private static string Prefix(string headline, string? because)
        => because is null ? headline : $"{headline} (because {because})";

    private static string Join(IReadOnlyList<AxisError> errors, Func<AxisError, string> pick)
        => errors.Count == 0 ? "(none)" : string.Join(", ", errors.Select(pick));
}
