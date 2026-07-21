namespace AxisUnitTests;

public class AxisResultCoreTests
{
    private static readonly AxisError Err1 = AxisError.NotFound("E1");
    private static readonly AxisError Err2 = AxisError.ValidationRule("E2");

    [Fact]
    public void Ok_Is_Success_And_Has_No_Errors()
    {
        var r = AxisResult.Ok();
        Assert.True(r.IsSuccess);
        Assert.False(r.IsFailure);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Ok_Generic_Exposes_Value()
    {
        var r = AxisResult.Ok(42);
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Error_Single_Creates_Failure()
    {
        var r = AxisResult.Error(Err1);
        Assert.True(r.IsFailure);
        Assert.False(r.IsSuccess);
        Assert.Single(r.Errors);
        Assert.Equal(Err1, r.Errors[0]);
    }

    [Fact]
    public void Error_Multiple_Creates_Failure_With_All()
    {
        var r = AxisResult.Error([Err1, Err2]);
        Assert.True(r.IsFailure);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void Error_Generic_Single_Creates_Failure()
    {
        var r = AxisResult.Error<int>(Err1);
        Assert.True(r.IsFailure);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void Error_Generic_Multiple_Creates_Failure()
    {
        var r = AxisResult.Error<int>([Err1, Err2]);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void Accessing_Value_On_Failure_Throws()
    {
        var r = AxisResult.Error<int>(Err1);
        var ex = Assert.Throws<NoAccessValueOnErrorResultException>(() => _ = r.Value);
        Assert.Single(ex.Errors);
        Assert.Contains("E1", ex.Message);
    }

    [Fact]
    public void JoinErrorCodes_Returns_Codes_Joined()
    {
        var r = AxisResult.Error([Err1, Err2]);
        Assert.Equal("E1, E2", r.JoinErrorCodes());
    }

    [Fact]
    public void JoinErrorCodes_With_Custom_Separator()
    {
        var r = AxisResult.Error([Err1, Err2]);
        Assert.Equal("E1 | E2", r.JoinErrorCodes(" | "));
    }

    [Fact]
    public void JoinErrorCodes_On_Success_Returns_Empty()
    {
        var r = AxisResult.Ok();
        Assert.Equal(string.Empty, r.JoinErrorCodes());
    }

    [Fact]
    public void Errors_On_Success_Returns_Empty_List()
    {
        var r = AxisResult.Ok();
        Assert.NotNull(r.Errors);
        Assert.Empty(r.Errors);
    }

    [Fact]
    public void Implicit_From_AxisError_To_AxisResult()
    {
        AxisResult r = Err1;
        Assert.True(r.IsFailure);
        Assert.Single(r.Errors);
    }

    [Fact]
    public void Implicit_From_List_To_AxisResult()
    {
        AxisResult r = new List<AxisError> { Err1, Err2 };
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void Implicit_From_Array_To_AxisResult()
    {
        AxisResult r = new[] { Err1, Err2 };
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void Implicit_From_Value_To_Generic_Result()
    {
        AxisResult<int> r = 7;
        Assert.True(r.IsSuccess);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public void Implicit_From_Error_To_Generic_Result()
    {
        AxisResult<int> r = Err1;
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Implicit_From_List_To_Generic_Result()
    {
        AxisResult<int> r = new List<AxisError> { Err1, Err2 };
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void Implicit_From_Array_To_Generic_Result()
    {
        AxisResult<int> r = new[] { Err1, Err2 };
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void NoAccessValueOnErrorResultException_Stores_Errors()
    {
        var errors = new[] { Err1, Err2 };
        var ex = new NoAccessValueOnErrorResultException(errors);
        Assert.Equal(2, ex.Errors.Count);
        Assert.Contains("2 error(s)", ex.Message);
        Assert.Contains("E1", ex.Message);
        Assert.Contains("E2", ex.Message);
    }
}
