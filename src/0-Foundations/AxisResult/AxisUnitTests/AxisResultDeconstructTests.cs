using System.Reflection;

namespace AxisUnitTests;

public class AxisResultDeconstructTests
{
    private static readonly AxisError E1 = AxisError.NotFound("NF");
    private static readonly AxisError E2 = AxisError.ValidationRule("VR");

    #region Deconstruct on AxisResult (non-generic)

    [Fact]
    public void Deconstruct_NonGeneric_Success_Yields_IsSuccess_True_And_Empty_Errors()
    {
        var (isSuccess, errors) = AxisResult.Ok();
        Assert.True(isSuccess);
        Assert.Empty(errors);
    }

    [Fact]
    public void Deconstruct_NonGeneric_Failure_Yields_IsSuccess_False_And_Errors()
    {
        var (isSuccess, errors) = AxisResult.Error(new[] { E1, E2 });
        Assert.False(isSuccess);
        Assert.Equal(2, errors.Count);
        Assert.Contains(E1, errors);
        Assert.Contains(E2, errors);
    }

    #endregion

    #region Deconstruct on AxisResult<TValue>

    [Fact]
    public void Deconstruct_Generic_Success_Yields_Value()
    {
        var (isSuccess, value, errors) = AxisResult.Ok(42);
        Assert.True(isSuccess);
        Assert.Equal(42, value);
        Assert.Empty(errors);
    }

    [Fact]
    public void Deconstruct_Generic_Failure_Yields_Default_Value_And_Errors()
    {
        var (isSuccess, value, errors) = AxisResult.Error<int>(E1);
        Assert.False(isSuccess);
        Assert.Equal(0, value);
        Assert.Single(errors);
        Assert.Equal(E1, errors[0]);
    }

    [Fact]
    public void Deconstruct_Generic_Failure_Reference_Type_Yields_Null()
    {
        var (isSuccess, value, errors) = AxisResult.Error<string>(E1);
        Assert.False(isSuccess);
        Assert.Null(value);
        Assert.Single(errors);
    }

    [Fact]
    public void Deconstruct_Generic_Enables_Positional_Pattern()
    {
        var result = AxisResult.Ok(10);
        var label = result is (true, 10, _) ? "ten" : "other";
        Assert.Equal("ten", label);
    }

    #endregion

    #region DebuggerDisplay

    [Fact]
    public void DebuggerDisplay_NonGeneric_Ok_Is_Ok()
    {
        var r = AxisResult.Ok();
        Assert.Equal("Ok", ReadDebuggerDisplay(r));
    }

    [Fact]
    public void DebuggerDisplay_NonGeneric_Error_Shows_Count_And_Codes()
    {
        var r = AxisResult.Error(new[] { E1, E2 });
        var display = ReadDebuggerDisplay(r);
        Assert.Contains("Error[2]", display);
        Assert.Contains("NF", display);
        Assert.Contains("VR", display);
    }

    [Fact]
    public void DebuggerDisplay_Generic_Ok_Shows_Value()
    {
        var r = AxisResult.Ok(123);
        var display = ReadDebuggerDisplay(r);
        Assert.Contains("Ok(", display);
        Assert.Contains("123", display);
    }

    [Fact]
    public void DebuggerDisplay_Generic_Error_Shows_Codes()
    {
        var r = AxisResult.Error<string>(E1);
        var display = ReadDebuggerDisplay(r);
        Assert.Contains("Error[1]", display);
        Assert.Contains("NF", display);
    }

    private static string ReadDebuggerDisplay(AxisResult result)
    {
        var prop = typeof(AxisResult).GetProperty(
            "DebuggerDisplay",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(prop);
        return (string)prop.GetValue(result)!;
    }

    #endregion
}
