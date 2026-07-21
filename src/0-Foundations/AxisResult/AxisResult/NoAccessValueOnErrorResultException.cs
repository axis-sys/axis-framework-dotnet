namespace Axis;

/// <summary>
/// Thrown by <see cref="AxisResult{TValue}.Value"/> when accessed on a failed result. Reaching this
/// exception means the railway was broken with an imperative <c>.Value</c> read instead of composing
/// with <c>Then</c>/<c>Map</c>/<c>Match</c>/<c>Recover</c>, which never throw and stay on the failure
/// rail instead.
/// </summary>
public sealed class NoAccessValueOnErrorResultException(IReadOnlyList<AxisError> errors) : InvalidOperationException(BuildMessage(errors))
{
    /// <summary>The errors carried by the failed result whose <c>Value</c> was accessed.</summary>
    public IReadOnlyList<AxisError> Errors { get; } = errors;

    private static string BuildMessage(IReadOnlyList<AxisError> errors)
    {
        var codes = string.Join(", ", errors.Select(e => e.Code));
        return $"Cannot access Value on a failed AxisResult. The result contains {errors.Count} error(s): {codes}";
    }
}
