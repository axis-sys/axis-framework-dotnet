using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace AxisResult.HttpResponse.UnitTests;

public class AxisProblemDetailsHttpExtensionsTests
{
    private const string TraceId = "trace-123";

    private static DefaultHttpContext ContextWithBufferedBody()
        => new() { TraceIdentifier = TraceId, Response = { Body = new MemoryStream() } };

    private static async Task<JsonDocument> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        return await JsonDocument.ParseAsync(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_writes_status_and_problem_details_straight_to_response()
    {
        var context = ContextWithBufferedBody();

        await context.WriteProblemDetailsAsync([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN")
        ]);

        Assert.Equal((int)HttpStatusCode.InternalServerError, context.Response.StatusCode);

        using var body = await ReadBodyAsync(context);
        Assert.Equal("https://axis.dev/problems/internal-server-error", body.RootElement.GetProperty("type").GetString());
        Assert.Equal(TraceId, body.RootElement.GetProperty("traceId").GetString());
        Assert.Equal("1 error(s) returned. 1 internal error(s) suppressed.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_single_error_overload_delegates_to_list_overload()
    {
        var context = ContextWithBufferedBody();

        await context.WriteProblemDetailsAsync(AxisError.Conflict("DUPLICATE"));

        Assert.Equal((int)HttpStatusCode.Conflict, context.Response.StatusCode);

        using var body = await ReadBodyAsync(context);
        Assert.Equal("https://axis.dev/problems/conflict", body.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public void ToProblemDetailsResult_wraps_problem_details_without_touching_the_response()
    {
        var context = ContextWithBufferedBody();

        var result = context.ToProblemDetailsResult([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN")
        ]);

        Assert.Equal((int)HttpStatusCode.InternalServerError, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("https://axis.dev/problems/internal-server-error", problem.Type);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);

        // The filter assigns context.Result; the MVC framework writes the body, not the extension.
        Assert.Equal(0, context.Response.Body.Length);
        Assert.Equal((int)HttpStatusCode.OK, context.Response.StatusCode);
    }

    [Fact]
    public void ToProblemDetailsResult_single_error_overload_delegates_to_list_overload()
    {
        var context = ContextWithBufferedBody();

        var result = context.ToProblemDetailsResult(AxisError.Forbidden("PERMISSION_DENIED"));

        Assert.Equal((int)HttpStatusCode.Forbidden, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("https://axis.dev/problems/forbidden", problem.Type);
    }

    [Fact]
    public async Task WriteProblemDetailsAsync_and_ToProblemDetailsResult_agree_on_status_and_body()
    {
        var errors = new[] { AxisError.NotFound("MISSING") };

        var writeContext = ContextWithBufferedBody();
        await writeContext.WriteProblemDetailsAsync(errors);
        using var written = await ReadBodyAsync(writeContext);

        var result = ContextWithBufferedBody().ToProblemDetailsResult(errors);
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(result.StatusCode, writeContext.Response.StatusCode);
        Assert.Equal(problem.Type, written.RootElement.GetProperty("type").GetString());
        Assert.Equal(problem.Detail, written.RootElement.GetProperty("detail").GetString());
    }
}
