namespace Axis;

/// <summary>
/// Holds the base URI used to build the <c>type</c> member of the <see cref="Microsoft.AspNetCore.Mvc.ProblemDetails"/>
/// produced by <see cref="AxisResultHttpExtensions"/>. Configured once at application startup; reads are lock-free.
/// </summary>
public static class AxisProblemDetailsConfiguration
{
    /// <summary>Default base URI applied when no override is configured.</summary>
    public const string DefaultProblemTypeBaseUri = "https://axis.dev/problems/";

    /// <summary>Current base URI prefixed to the kebab-cased error type (e.g. <c>{base}unauthorized</c>).</summary>
    public static string ProblemTypeBaseUri { get; private set; } = DefaultProblemTypeBaseUri;

    /// <summary>
    /// Overrides the base URI of the <c>type</c> member. A null/blank value is ignored (keeps the current value);
    /// a trailing slash is added when missing. Intended to be called a single time during startup.
    /// </summary>
    /// <param name="baseUri">The new base URI, e.g. <c>https://problems.example.test/</c>.</param>
    public static void ConfigureProblemTypeBaseUri(string? baseUri)
    {
        if (string.IsNullOrWhiteSpace(baseUri))
            return;

        ProblemTypeBaseUri = baseUri.EndsWith('/') ? baseUri : baseUri + "/";
    }
}
