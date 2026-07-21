namespace Axis;

/// <summary>
/// The 12 typed error categories an <see cref="AxisError"/> can carry. Each category maps to a fixed
/// HTTP status at the presentation edge (see <c>AxisResult.HttpResponse</c>); the enum itself carries
/// no HTTP knowledge, only the classification.
/// </summary>
public enum AxisErrorType
{
    /// <summary>Downstream dependency is temporarily unavailable. Transient (<see cref="AxisError.IsTransient"/>). HTTP 503.</summary>
    ServiceUnavailable = 1,

    /// <summary>An operation exceeded its allotted time. Transient (<see cref="AxisError.IsTransient"/>). HTTP 504.</summary>
    Timeout = 2,

    /// <summary>Caller exceeded a rate limit. Transient (<see cref="AxisError.IsTransient"/>). HTTP 429.</summary>
    TooManyRequests = 3,

    /// <summary>An upstream gateway did not receive a timely response from a further upstream server. Transient (<see cref="AxisError.IsTransient"/>). HTTP 504.</summary>
    GatewayTimeout = 4,

    /// <summary>Input failed a validation rule (shape, format, required field). HTTP 400.</summary>
    ValidationRule = 5,

    /// <summary>The requested entity does not exist. HTTP 404.</summary>
    NotFound = 6,

    /// <summary>The operation collides with the current state of the entity (e.g. duplicate create). HTTP 409.</summary>
    Conflict = 7,

    /// <summary>Input is well-formed but violates a domain/business invariant. HTTP 422.</summary>
    BusinessRule = 8,

    /// <summary>The caller is not authenticated. HTTP 401.</summary>
    Unauthorized = 9,

    /// <summary>The caller is authenticated but lacks permission for the operation. HTTP 403.</summary>
    Forbidden = 10,

    /// <summary>A mapping/translation between representations failed. HTTP 500.</summary>
    Mapping = 11,

    /// <summary>An unexpected internal failure. HTTP 500.</summary>
    InternalServerError = 12
}
