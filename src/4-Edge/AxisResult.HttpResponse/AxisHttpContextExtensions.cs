using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Axis;

public static class AxisHttpContextExtensions
{
    extension(HttpContext context)
    {
        public async Task<IActionResult> SendAsync(Task<AxisResult> resultTask, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(resultTask);
            var result = await resultTask;
            return result.SendHttpResponse(context.TraceIdentifier, statusCode);
        }

        public async Task<IActionResult> SendAsync<TData>(Task<AxisResult<TData>> resultTask, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(resultTask);
            var result = await resultTask;
            return result.SendHttpResponse(context.TraceIdentifier, statusCode);
        }
    }
}