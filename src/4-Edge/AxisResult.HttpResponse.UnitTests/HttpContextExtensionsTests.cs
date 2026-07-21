using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AxisResult.HttpResponse.UnitTests;

public class AxisResultHttpExtensionsContextTests
{
    private const string TraceId = "trace-123";

    [Fact]
    public async Task SendAsync_HttpContext_nongeneric_success_default_200()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Ok())
        );

        var statusResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.OK, statusResult.StatusCode);
    }

    [Fact]
    public async Task SendAsync_HttpContext_nongeneric_success_custom_status()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Ok()),
            HttpStatusCode.Created
        );

        var statusResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Created, statusResult.StatusCode);
    }

    [Fact]
    public async Task SendAsync_HttpContext_nongeneric_failure_returns_problem_details()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Error(AxisError.NotFound("MISSING")))
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_success_default_200()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Ok("payload"))
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.OK, objectResult.StatusCode);
        Assert.Equal("payload", objectResult.Value);
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_success_custom_status()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Ok("test-data")),
            HttpStatusCode.Created
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Created, objectResult.StatusCode);
        Assert.Equal("test-data", objectResult.Value);
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_success_no_content()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Ok("payload")),
            HttpStatusCode.NoContent
        );

        Assert.IsType<NoContentResult>(actionResult);
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_failure_returns_problem_details()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Error<string>(AxisError.Conflict("DUPLICATE")))
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Conflict, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task SendAsync_HttpContext_uses_trace_identifier()
    {
        var customTraceId = "custom-trace-456";
        var context = new DefaultHttpContext { TraceIdentifier = customTraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Error<string>(AxisError.ValidationRule("INVALID")))
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(customTraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public async Task SendAsync_HttpContext_nongeneric_throws_on_null_context()
    {
        HttpContext context = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.SendAsync(Task.FromResult(Axis.AxisResult.Ok()))
        );
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_throws_on_null_context()
    {
        HttpContext context = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.SendAsync(Task.FromResult(Axis.AxisResult.Ok("test")))
        );
    }

    [Fact]
    public async Task SendAsync_HttpContext_nongeneric_throws_on_null_task()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.SendAsync(null!)
        );
    }

    [Fact]
    public async Task SendAsync_HttpContext_generic_throws_on_null_task()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => context.SendAsync((Task<AxisResult<string>>)null!)
        );
    }

    [Fact]
    public async Task SendAsync_HttpContext_awaits_pending_task()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };
        var pendingResult = new TaskCompletionSource<AxisResult<string>>();

        var actionResultTask = context.SendAsync(pendingResult.Task);
        Assert.False(actionResultTask.IsCompleted);

        pendingResult.SetResult(Axis.AxisResult.Ok("async-data"));
        var actionResult = await actionResultTask;

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal("async-data", objectResult.Value);
    }

    [Fact]
    public async Task SendAsync_HttpContext_picks_most_severe_error()
    {
        var context = new DefaultHttpContext { TraceIdentifier = TraceId };

        var actionResult = await context.SendAsync(
            Task.FromResult(Axis.AxisResult.Error<string>([
                AxisError.ValidationRule("FIELD_INVALID"),
                AxisError.InternalServerError("DB_DOWN")
            ]))
        );

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }
}
