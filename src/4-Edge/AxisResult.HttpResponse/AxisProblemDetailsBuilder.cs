using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace Axis;

/// <summary>
/// Converts <see cref="AxisError"/> collections into RFC 7807 <see cref="ProblemDetails"/>, decoupled
/// from <see cref="Microsoft.AspNetCore.Mvc.IActionResult"/>. Used by <see cref="AxisResultHttpExtensions"/>
/// to render controller responses, and reusable as-is by middleware and authorization filters that need
/// the same severity-based status selection outside the MVC action pipeline. To consume the result from
/// an <c>HttpContext</c> — write it onto the response, or wrap it in an <see cref="ObjectResult"/> —
/// see <see cref="AxisProblemDetailsHttpExtensions"/>.
/// </summary>
public static class AxisProblemDetailsBuilder
{
    /// <summary>
    /// Builds the HTTP status code and <see cref="ProblemDetails"/> for a failed <c>AxisResult</c>.
    /// Picks the status of the most severe error (<see cref="AxisErrorTypeHttpMapping.Severity"/>);
    /// <see cref="AxisErrorType.InternalServerError"/> entries are counted but never exposed in the body.
    /// </summary>
    /// <param name="errors">The errors carried by the failed result. Never empty in the normal
    /// <c>AxisResult</c> flow (a failure without errors is a defensive fallback below).</param>
    /// <param name="traceId">The request trace id, echoed back in <c>Extensions["traceId"]</c>.</param>
    public static (int StatusCode, ProblemDetails Details) Build(IReadOnlyList<AxisError> errors, string traceId)
    {
        if (errors.Count == 0)
        {
            var fallback = new ProblemDetails
            {
                Type = AxisProblemDetailsConfiguration.ProblemTypeBaseUri + "internal-server-error",
                Title = HttpStatusCode.InternalServerError.ToProblemTitle(),
                Status = (int)HttpStatusCode.InternalServerError,
                Detail = "Failure without errors.",
                Extensions = { ["traceId"] = traceId }
            };
            return ((int)HttpStatusCode.InternalServerError, fallback);
        }

        var visibleErrors = new List<AxisError>(errors.Count);
        var internalErrors = new List<AxisError>();
        var maxSeverity = int.MinValue;
        var maxType = errors[0].Type;
        foreach (var error in errors)
        {
            if (error.Type == AxisErrorType.InternalServerError)
                internalErrors.Add(error);
            else
                visibleErrors.Add(error);

            var severity = error.Type.Severity();
            if (severity <= maxSeverity)
                continue;

            maxSeverity = severity;
            maxType = error.Type;
        }

        var httpStatusCode = maxType.ToHttpStatusCode();
        var statusCodeInt = (int)httpStatusCode;

        var problemDetails = new ProblemDetails
        {
            Type = AxisProblemDetailsConfiguration.ProblemTypeBaseUri + ToKebabCase(maxType.ToString()),
            Title = httpStatusCode.ToProblemTitle(),
            Status = statusCodeInt,
            Detail = $"{visibleErrors.Count} error(s) returned. {internalErrors.Count} internal error(s) suppressed.",
            Extensions =
            {
                ["traceId"] = traceId,
                ["errors"] = visibleErrors.Select(e => new
                {
                    code = e.Code,
                    type = e.Type.ToString()
                }).ToArray()
            }
        };

        return (statusCodeInt, problemDetails);
    }

    private static string ToKebabCase(string pascalCase)
    {
        if (string.IsNullOrEmpty(pascalCase))
            return pascalCase;

        var sb = new StringBuilder(pascalCase.Length + 8);
        for (var i = 0; i < pascalCase.Length; i++)
        {
            var c = pascalCase[i];
            if (char.IsUpper(c) && i > 0)
                sb.Append('-');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
