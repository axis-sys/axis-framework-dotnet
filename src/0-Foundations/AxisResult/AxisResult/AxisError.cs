using System.Diagnostics;

namespace Axis;

/// <summary>
/// A failure as a <b>value</b> — never an exception. Carries exactly two things: a stable
/// <see cref="Code"/> (an UPPER_SNAKE_CASE key, safe for logs/metrics/retry policies) and a
/// <see cref="Type"/> category. Deliberately has no <c>Message</c> field: localization and
/// presentation belong to a <c>code → message</c> resolver at the edge, not to the value that
/// travels through the pipeline. Construct one with the typed factory matching the failure
/// (<see cref="NotFound"/>, <see cref="Conflict"/>, <see cref="ValidationRule"/>, ...); each maps to
/// a fixed HTTP status at the presentation edge.
/// </summary>
[DebuggerDisplay("{Type,nq} {Code,nq}")]
public record AxisError
{
    /// <summary>
    /// Sentinel code used when an <see cref="AxisError"/> is constructed with a null, empty or
    /// whitespace code. A Result library must never throw from its own factory — this sentinel
    /// keeps the "errors as values" invariant while still signalling the programmer bug loudly.
    /// </summary>
    public const string MissingCodeSentinel = "AXIS_ERROR_CODE_MISSING";

    private AxisError(string code, AxisErrorType type)
    {
        Code = string.IsNullOrWhiteSpace(code) ? MissingCodeSentinel : code;
        Type = type;
    }

    /// <summary>
    /// Stable, UPPER_SNAKE_CASE key for this failure (e.g. <c>"USER_NOT_FOUND"</c>). Safe to log,
    /// alert and pivot retry policies on without parsing prose. Falls back to
    /// <see cref="MissingCodeSentinel"/> when constructed with a null/empty/whitespace code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// The error's category. Drives <see cref="IsTransient"/> and the HTTP status mapping at the
    /// presentation edge; never inspect <see cref="Code"/> to infer this.
    /// </summary>
    public AxisErrorType Type { get; }

    /// <summary>
    /// True for the four categories worth a silent retry/backoff — <see cref="AxisErrorType.ServiceUnavailable"/>,
    /// <see cref="AxisErrorType.Timeout"/>, <see cref="AxisErrorType.TooManyRequests"/> and
    /// <see cref="AxisErrorType.GatewayTimeout"/> — instead of surfacing as a terminal failure. See also
    /// <see cref="AxisResult.IsTransientFailure"/>, the same question asked of a whole result.
    /// </summary>
    public bool IsTransient => Type is AxisErrorType.ServiceUnavailable
        or AxisErrorType.Timeout
        or AxisErrorType.TooManyRequests
        or AxisErrorType.GatewayTimeout;

    /// <summary>Creates an unexpected internal failure (<see cref="AxisErrorType.InternalServerError"/>, HTTP 500).</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError InternalServerError(string code) => new(code, AxisErrorType.InternalServerError);

    /// <summary>Creates a validation failure (<see cref="AxisErrorType.ValidationRule"/>, HTTP 400) — malformed or missing input.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError ValidationRule(string code) => new(code, AxisErrorType.ValidationRule);

    /// <summary>Creates a not-found failure (<see cref="AxisErrorType.NotFound"/>, HTTP 404) — the requested entity does not exist.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError NotFound(string code) => new(code, AxisErrorType.NotFound);

    /// <summary>Creates a conflict failure (<see cref="AxisErrorType.Conflict"/>, HTTP 409) — collides with the entity's current state (e.g. duplicate create).</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError Conflict(string code) => new(code, AxisErrorType.Conflict);

    /// <summary>Creates a business-rule failure (<see cref="AxisErrorType.BusinessRule"/>, HTTP 422) — well-formed input that violates a domain invariant.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError BusinessRule(string code) => new(code, AxisErrorType.BusinessRule);

    /// <summary>Creates an unauthorized failure (<see cref="AxisErrorType.Unauthorized"/>, HTTP 401) — the caller is not authenticated.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError Unauthorized(string code) => new(code, AxisErrorType.Unauthorized);

    /// <summary>Creates a forbidden failure (<see cref="AxisErrorType.Forbidden"/>, HTTP 403) — the caller is authenticated but lacks permission.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError Forbidden(string code) => new(code, AxisErrorType.Forbidden);

    /// <summary>Creates a service-unavailable failure (<see cref="AxisErrorType.ServiceUnavailable"/>, HTTP 503). Transient — see <see cref="IsTransient"/>.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError ServiceUnavailable(string code) => new(code, AxisErrorType.ServiceUnavailable);

    /// <summary>Creates a timeout failure (<see cref="AxisErrorType.Timeout"/>, HTTP 504). Transient — see <see cref="IsTransient"/>.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError Timeout(string code) => new(code, AxisErrorType.Timeout);

    /// <summary>Creates a too-many-requests failure (<see cref="AxisErrorType.TooManyRequests"/>, HTTP 429) — the caller exceeded a rate limit. Transient — see <see cref="IsTransient"/>.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError TooManyRequests(string code) => new(code, AxisErrorType.TooManyRequests);

    /// <summary>Creates a gateway-timeout failure (<see cref="AxisErrorType.GatewayTimeout"/>, HTTP 504) — an upstream gateway did not get a timely response further upstream. Transient — see <see cref="IsTransient"/>.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError GatewayTimeout(string code) => new(code, AxisErrorType.GatewayTimeout);

    /// <summary>Creates a mapping failure (<see cref="AxisErrorType.Mapping"/>, HTTP 500) — translation between representations failed.</summary>
    /// <param name="code">Stable UPPER_SNAKE_CASE key identifying the failure.</param>
    public static AxisError Mapping(string code) => new(code, AxisErrorType.Mapping);
}
