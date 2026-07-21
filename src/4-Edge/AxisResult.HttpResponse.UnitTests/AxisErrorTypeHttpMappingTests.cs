using System.Net;

namespace AxisResult.HttpResponse.UnitTests;

public class AxisErrorTypeHttpMappingTests
{
    [Theory]
    [InlineData(AxisErrorType.ValidationRule, HttpStatusCode.BadRequest)]
    [InlineData(AxisErrorType.NotFound, HttpStatusCode.NotFound)]
    [InlineData(AxisErrorType.Conflict, HttpStatusCode.Conflict)]
    [InlineData(AxisErrorType.BusinessRule, HttpStatusCode.UnprocessableEntity)]
    [InlineData(AxisErrorType.Unauthorized, HttpStatusCode.Unauthorized)]
    [InlineData(AxisErrorType.Forbidden, HttpStatusCode.Forbidden)]
    [InlineData(AxisErrorType.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(AxisErrorType.Timeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(AxisErrorType.GatewayTimeout, HttpStatusCode.GatewayTimeout)]
    [InlineData(AxisErrorType.InternalServerError, HttpStatusCode.InternalServerError)]
    [InlineData(AxisErrorType.Mapping, HttpStatusCode.InternalServerError)]
    public void ToHttpStatusCodeMapsKnownTypes(AxisErrorType type, HttpStatusCode expected)
        => Assert.Equal(expected, type.ToHttpStatusCode());

    [Fact]
    public void ToHttpStatusCodeMapsTooManyRequestsTo429()
        => Assert.Equal((HttpStatusCode)429, AxisErrorType.TooManyRequests.ToHttpStatusCode());

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Bad Request")]
    [InlineData(HttpStatusCode.Unauthorized, "Unauthorized")]
    [InlineData(HttpStatusCode.Forbidden, "Forbidden")]
    [InlineData(HttpStatusCode.NotFound, "Not Found")]
    [InlineData(HttpStatusCode.Conflict, "Conflict")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Unprocessable Entity")]
    [InlineData(HttpStatusCode.InternalServerError, "Internal Server Error")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Service Unavailable")]
    [InlineData(HttpStatusCode.GatewayTimeout, "Gateway Timeout")]
    [InlineData(HttpStatusCode.OK, "Error")]
    public void ToProblemTitleMapsKnownStatusCodes(HttpStatusCode statusCode, string expected)
        => Assert.Equal(expected, statusCode.ToProblemTitle());

    [Fact]
    public void ToProblemTitleMapsTooManyRequests()
        => Assert.Equal("Too Many Requests", ((HttpStatusCode)429).ToProblemTitle());

    [Theory]
    [InlineData(AxisErrorType.InternalServerError, 100)]
    [InlineData(AxisErrorType.ServiceUnavailable, 90)]
    [InlineData(AxisErrorType.GatewayTimeout, 85)]
    [InlineData(AxisErrorType.Timeout, 80)]
    [InlineData(AxisErrorType.Unauthorized, 70)]
    [InlineData(AxisErrorType.Forbidden, 65)]
    [InlineData(AxisErrorType.Conflict, 60)]
    [InlineData(AxisErrorType.BusinessRule, 55)]
    [InlineData(AxisErrorType.NotFound, 50)]
    [InlineData(AxisErrorType.TooManyRequests, 45)]
    [InlineData(AxisErrorType.ValidationRule, 40)]
    [InlineData(AxisErrorType.Mapping, 95)]
    public void SeverityValuesMatchExpected(AxisErrorType type, int expected)
        => Assert.Equal(expected, type.Severity());
}
