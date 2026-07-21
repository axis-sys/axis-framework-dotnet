using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Axis;

/// <summary>
/// Consumes <see cref="AxisProblemDetailsBuilder.Build"/> from an <see cref="HttpContext"/>, for code
/// that runs outside the MVC action pipeline and therefore cannot return an
/// <see cref="Microsoft.AspNetCore.Mvc.IActionResult"/> the way a controller does:
/// <see cref="WriteProblemDetailsAsync(HttpContext, IReadOnlyList{AxisError})"/> for middleware, which
/// writes the response itself, and <see cref="ToProblemDetailsResult(HttpContext, IReadOnlyList{AxisError})"/>
/// for an <c>IAsyncAuthorizationFilter</c>, which assigns a result instead. Both apply the same
/// severity-based status selection <see cref="AxisHttpContextExtensions"/> applies at the controller.
/// </summary>
public static class AxisProblemDetailsHttpExtensions
{
    extension(HttpContext context)
    {
        /// <summary>
        /// Single-error convenience overload of
        /// <see cref="WriteProblemDetailsAsync(HttpContext, IReadOnlyList{AxisError})"/>.
        /// </summary>
        /// <param name="error">The error to render.</param>
        public Task WriteProblemDetailsAsync(AxisError error)
            => context.WriteProblemDetailsAsync([error]);

        /// <summary>
        /// Sets <see cref="HttpResponse.StatusCode"/> and serializes the <see cref="ProblemDetails"/>
        /// body onto the response. Unlike <c>HttpContext.SendAsync</c> — which returns an
        /// <see cref="Microsoft.AspNetCore.Mvc.IActionResult"/> for MVC to send later — this writes the
        /// response itself, so the caller must not go on to write it again.
        /// </summary>
        /// <param name="errors">The errors carried by the failed result. See
        /// <see cref="AxisProblemDetailsBuilder.Build"/> for the empty-list fallback.</param>
        public Task WriteProblemDetailsAsync(IReadOnlyList<AxisError> errors)
        {
            var (statusCode, details) = AxisProblemDetailsBuilder.Build(errors, context.TraceIdentifier);
            context.Response.StatusCode = statusCode;
            return context.Response.WriteAsJsonAsync(details);
        }

        /// <summary>
        /// Single-error convenience overload of
        /// <see cref="ToProblemDetailsResult(HttpContext, IReadOnlyList{AxisError})"/>.
        /// </summary>
        /// <param name="error">The error to render.</param>
        public ObjectResult ToProblemDetailsResult(AxisError error)
            => context.ToProblemDetailsResult([error]);

        /// <summary>
        /// Wraps the built <see cref="ProblemDetails"/> in an <see cref="ObjectResult"/> without writing
        /// to the response — for an <c>IAsyncAuthorizationFilter</c>, which short-circuits MVC by
        /// assigning <c>context.Result</c> rather than writing the body itself.
        /// </summary>
        /// <param name="errors">The errors carried by the failed result. See
        /// <see cref="AxisProblemDetailsBuilder.Build"/> for the empty-list fallback.</param>
        public ObjectResult ToProblemDetailsResult(IReadOnlyList<AxisError> errors)
        {
            var (statusCode, details) = AxisProblemDetailsBuilder.Build(errors, context.TraceIdentifier);
            return new ObjectResult(details) { StatusCode = statusCode };
        }
    }
}
