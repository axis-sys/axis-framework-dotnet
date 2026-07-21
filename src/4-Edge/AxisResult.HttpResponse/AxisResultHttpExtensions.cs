using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace Axis;

internal static class AxisResultHttpExtensions
{
    public static IActionResult SendHttpResponse(this AxisResult result, string traceId, HttpStatusCode successStatusCode = HttpStatusCode.OK)
    {
        if (result.IsSuccess)
            return new StatusCodeResult((int)successStatusCode);

        return BuildProblemDetailsResult(result.Errors, traceId);
    }

    public static IActionResult SendHttpResponse<TData>(this AxisResult<TData> result, string traceId, HttpStatusCode successStatusCode = HttpStatusCode.OK)
    {
        if (result.IsFailure)
            return BuildProblemDetailsResult(result.Errors, traceId);

        if (successStatusCode == HttpStatusCode.NoContent)
            return new NoContentResult();

        return new ObjectResult(result.Value) { StatusCode = (int)successStatusCode };
    }

    private static ObjectResult BuildProblemDetailsResult(IReadOnlyList<AxisError> errors, string traceId)
    {
        var (statusCode, details) = AxisProblemDetailsBuilder.Build(errors, traceId);
        return new ObjectResult(details) { StatusCode = statusCode };
    }
}
