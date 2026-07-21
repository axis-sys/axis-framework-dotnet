using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace AxisResult.HttpResponse.UnitTests;

public class AxisResultHttpExtensionsTests
{
    private const string TraceId = "trace-123";

    [Fact]
    public void Success_NonGeneric_returns_StatusCodeResult_with_default_200()
    {
        var result = Axis.AxisResult.Ok();

        var actionResult = result.SendHttpResponse(TraceId);

        var statusResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.OK, statusResult.StatusCode);
    }

    [Fact]
    public void Success_NonGeneric_uses_provided_status_code()
    {
        var result = Axis.AxisResult.Ok();

        var actionResult = result.SendHttpResponse(TraceId, HttpStatusCode.Created);

        var statusResult = Assert.IsType<StatusCodeResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Created, statusResult.StatusCode);
    }

    [Fact]
    public void Success_Generic_with_NoContent_returns_NoContentResult()
    {
        var result = Axis.AxisResult.Ok("payload");

        var actionResult = result.SendHttpResponse(TraceId, HttpStatusCode.NoContent);

        Assert.IsType<NoContentResult>(actionResult);
    }

    [Fact]
    public void Success_Generic_returns_ObjectResult_with_value()
    {
        var result = Axis.AxisResult.Ok("payload");

        var actionResult = result.SendHttpResponse(TraceId, HttpStatusCode.Created);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.Created, objectResult.StatusCode);
        Assert.Equal("payload", objectResult.Value);
    }

    [Fact]
    public void Failure_Generic_returns_ProblemDetails_result()
    {
        var result = Axis.AxisResult.Error<string>(AxisError.NotFound("MISSING"));

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.NotFound, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://axis.dev/problems/not-found", problem.Type);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public void Failure_picks_most_severe_error_status()
    {
        var result = Axis.AxisResult.Error([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN")
        ]);

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.InternalServerError, objectResult.StatusCode);
    }

    [Fact]
    public void Failure_suppresses_internal_errors_from_payload_but_counts_them()
    {
        var result = Axis.AxisResult.Error([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN"),
            AxisError.InternalServerError("CACHE_DOWN")
        ]);

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("1 error(s) returned. 2 internal error(s) suppressed.", problem.Detail);

        var visible = Assert.IsAssignableFrom<IEnumerable<object>>(problem.Extensions["errors"]!);
        Assert.Single(visible);
    }

    [Fact]
    public void Failure_exposes_traceId_in_extensions()
    {
        var result = Axis.AxisResult.Error(AxisError.NotFound("MISSING"));

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public void Failure_uses_kebab_case_problem_type_uri()
    {
        var result = Axis.AxisResult.Error(AxisError.InternalServerError("DB_DOWN"));

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://axis.dev/problems/internal-server-error", problem.Type);
        Assert.Equal("Internal Server Error", problem.Title);
        Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
    }

    [Fact]
    public void Failure_validation_only_returns_400_with_visible_error()
    {
        var result = Axis.AxisResult.Error(AxisError.ValidationRule("EMAIL_INVALID"));

        var actionResult = result.SendHttpResponse(TraceId);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal((int)HttpStatusCode.BadRequest, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("https://axis.dev/problems/validation-rule", problem.Type);
        var visible = Assert.IsAssignableFrom<IEnumerable<object>>(problem.Extensions["errors"]!);
        Assert.Single(visible);
    }

    [Theory]
    [InlineData(AxisErrorType.InternalServerError, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.ServiceUnavailable, AxisErrorType.NotFound)]
    [InlineData(AxisErrorType.GatewayTimeout, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.Timeout, AxisErrorType.NotFound)]
    [InlineData(AxisErrorType.Unauthorized, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.Forbidden, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.Conflict, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.BusinessRule, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.NotFound, AxisErrorType.ValidationRule)]
    [InlineData(AxisErrorType.TooManyRequests, AxisErrorType.ValidationRule)]
    public void Severity_higher_type_wins(AxisErrorType higher, AxisErrorType lower)
        => Assert.True(higher.Severity() > lower.Severity());

    // Global static state: these tests live in this class (whose methods run sequentially)
    // so they don't collide with the "axis.dev" asserts above, and they restore the default in the finally.
    [Fact]
    public void Failure_uses_configured_problem_type_base_uri_and_appends_trailing_slash()
    {
        try
        {
            AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri("https://problems.example.test");

            var result = Axis.AxisResult.Error(AxisError.NotFound("MISSING"));

            var actionResult = result.SendHttpResponse(TraceId);

            var objectResult = Assert.IsType<ObjectResult>(actionResult);
            var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal("https://problems.example.test/not-found", problem.Type);
        }
        finally
        {
            AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri(
                AxisProblemDetailsConfiguration.DefaultProblemTypeBaseUri);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ConfigureProblemTypeBaseUri_ignores_null_or_blank_and_keeps_default(string? input)
    {
        AxisProblemDetailsConfiguration.ConfigureProblemTypeBaseUri(input);

        Assert.Equal(
            AxisProblemDetailsConfiguration.DefaultProblemTypeBaseUri,
            AxisProblemDetailsConfiguration.ProblemTypeBaseUri);
    }
}
