using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Reflection;

namespace AxisResult.HttpResponse.UnitTests;

public class AxisProblemDetailsBuilderTests
{
    private const string TraceId = "trace-123";

    [Fact]
    public void Build_picks_most_severe_error_status()
    {
        var (statusCode, _) = AxisProblemDetailsBuilder.Build([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN")
        ], TraceId);

        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
    }

    [Fact]
    public void Build_suppresses_internal_errors_from_payload_but_counts_them()
    {
        var (_, details) = AxisProblemDetailsBuilder.Build([
            AxisError.ValidationRule("FIELD_INVALID"),
            AxisError.InternalServerError("DB_DOWN"),
            AxisError.InternalServerError("CACHE_DOWN")
        ], TraceId);

        Assert.Equal("1 error(s) returned. 2 internal error(s) suppressed.", details.Detail);

        var visible = Assert.IsAssignableFrom<IEnumerable<object>>(details.Extensions["errors"]!);
        Assert.Single(visible);
    }

    [Fact]
    public void Build_exposes_traceId_in_extensions()
    {
        var (_, details) = AxisProblemDetailsBuilder.Build([AxisError.NotFound("MISSING")], TraceId);

        Assert.Equal(TraceId, details.Extensions["traceId"]);
    }

    [Fact]
    public void Build_uses_kebab_case_problem_type_uri()
    {
        var (statusCode, details) = AxisProblemDetailsBuilder.Build([AxisError.InternalServerError("DB_DOWN")], TraceId);

        Assert.Equal("https://axis.dev/problems/internal-server-error", details.Type);
        Assert.Equal("Internal Server Error", details.Title);
        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
        Assert.Equal(statusCode, details.Status);
    }

    [Fact]
    public void Build_validation_only_returns_400_with_visible_error()
    {
        var (statusCode, details) = AxisProblemDetailsBuilder.Build([AxisError.ValidationRule("EMAIL_INVALID")], TraceId);

        Assert.Equal((int)HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("https://axis.dev/problems/validation-rule", details.Type);
        var visible = Assert.IsAssignableFrom<IEnumerable<object>>(details.Extensions["errors"]!);
        Assert.Single(visible);
    }

    // Defensive path: a failed AxisResult always carries >= 1 error (see AxisResult.cs:
    // _errors becomes null when the list is empty and IsSuccess turns true). So the
    // "errors.Count == 0" branch is unreachable through the public AxisResult flow, but a caller
    // off that track (middleware/filter building the list by hand) can trigger the fallback.
    [Fact]
    public void Build_with_no_errors_returns_internal_server_error_fallback()
    {
        var (statusCode, details) = AxisProblemDetailsBuilder.Build([], TraceId);

        Assert.Equal((int)HttpStatusCode.InternalServerError, statusCode);
        Assert.Equal("https://axis.dev/problems/internal-server-error", details.Type);
        Assert.Equal("Internal Server Error", details.Title);
        Assert.Equal((int)HttpStatusCode.InternalServerError, details.Status);
        Assert.Equal("Failure without errors.", details.Detail);
        Assert.Equal(TraceId, details.Extensions["traceId"]);
    }

    // Defensive path: ToKebabCase is only called with maxType.ToString(), which is never empty.
    // The string.IsNullOrEmpty guard can only be exercised by invoking the private method directly.
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ToKebabCase_returns_input_unchanged_when_null_or_empty(string? input)
    {
        var method = typeof(AxisProblemDetailsBuilder).GetMethod(
            "ToKebabCase",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = (string?)method.Invoke(null, [input]);

        Assert.Equal(input, result);
    }

    [Fact]
    public void Build_matches_SendHttpResponse_output_bit_for_bit()
    {
        var errors = new[] { AxisError.NotFound("MISSING") };

        var (builderStatus, builderDetails) = AxisProblemDetailsBuilder.Build(errors, TraceId);

        var actionResult = Axis.AxisResult.Error(errors[0]).SendHttpResponse(TraceId);
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        var extensionDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

        Assert.Equal(builderStatus, objectResult.StatusCode);
        Assert.Equal(builderDetails.Type, extensionDetails.Type);
        Assert.Equal(builderDetails.Title, extensionDetails.Title);
        Assert.Equal(builderDetails.Status, extensionDetails.Status);
        Assert.Equal(builderDetails.Detail, extensionDetails.Detail);
        Assert.Equal(builderDetails.Extensions["traceId"], extensionDetails.Extensions["traceId"]);
    }
}
