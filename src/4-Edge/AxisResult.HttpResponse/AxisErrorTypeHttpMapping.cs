using System.Net;

namespace Axis;

internal static class AxisErrorTypeHttpMapping
{
    internal static HttpStatusCode ToHttpStatusCode(this AxisErrorType axisErrorType) => axisErrorType switch
    {
        AxisErrorType.ValidationRule => HttpStatusCode.BadRequest,
        AxisErrorType.NotFound => HttpStatusCode.NotFound,
        AxisErrorType.Conflict => HttpStatusCode.Conflict,
        AxisErrorType.BusinessRule => HttpStatusCode.UnprocessableEntity,
        AxisErrorType.Unauthorized => HttpStatusCode.Unauthorized,
        AxisErrorType.Forbidden => HttpStatusCode.Forbidden,
        AxisErrorType.ServiceUnavailable => HttpStatusCode.ServiceUnavailable,
        AxisErrorType.Timeout => HttpStatusCode.GatewayTimeout,
        AxisErrorType.TooManyRequests => (HttpStatusCode)429,
        AxisErrorType.GatewayTimeout => HttpStatusCode.GatewayTimeout,
        _ => HttpStatusCode.InternalServerError
    };

    internal static string ToProblemTitle(this HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => "Bad Request",
        HttpStatusCode.Unauthorized => "Unauthorized",
        HttpStatusCode.Forbidden => "Forbidden",
        HttpStatusCode.NotFound => "Not Found",
        HttpStatusCode.Conflict => "Conflict",
        HttpStatusCode.UnprocessableEntity => "Unprocessable Entity",
        (HttpStatusCode)429 => "Too Many Requests",
        HttpStatusCode.InternalServerError => "Internal Server Error",
        HttpStatusCode.ServiceUnavailable => "Service Unavailable",
        HttpStatusCode.GatewayTimeout => "Gateway Timeout",
        _ => "Error"
    };

    internal static int Severity(this AxisErrorType type) => type switch
    {
        AxisErrorType.InternalServerError => 100,
        AxisErrorType.ServiceUnavailable => 90,
        AxisErrorType.GatewayTimeout => 85,
        AxisErrorType.Timeout => 80,
        AxisErrorType.Unauthorized => 70,
        AxisErrorType.Forbidden => 65,
        AxisErrorType.Conflict => 60,
        AxisErrorType.BusinessRule => 55,
        AxisErrorType.NotFound => 50,
        AxisErrorType.TooManyRequests => 45,
        AxisErrorType.ValidationRule => 40,
        _ => 95
    };
}
