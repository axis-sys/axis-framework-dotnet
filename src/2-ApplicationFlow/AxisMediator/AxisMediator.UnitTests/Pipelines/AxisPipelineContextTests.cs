using AxisMediator.Contracts.Pipelines;

namespace AxisMediator.UnitTests.Pipelines;

public class AxisPipelineContextTests
{
    [Fact]
    public void Set_ThenGet_ReturnsValue()
    {
        var context = new AxisPipelineContext();

        context.Set("key", 42);
        var result = context.Get<int>("key");

        Assert.Equal(42, result);
    }

    [Fact]
    public void Set_ThenGet_StringValue_ReturnsValue()
    {
        var context = new AxisPipelineContext();

        context.Set("name", "hello");
        var result = context.Get<string>("name");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Get_MissingKey_ReturnsDefault()
    {
        var context = new AxisPipelineContext();

        var result = context.Get<int>("missing");

        Assert.Equal(default, result);
    }

    [Fact]
    public void Get_MissingKey_ReferenceType_ReturnsNull()
    {
        var context = new AxisPipelineContext();

        var result = context.Get<string>("missing");

        Assert.Null(result);
    }

    [Fact]
    public void Get_WrongType_ReturnsDefault()
    {
        var context = new AxisPipelineContext();
        context.Set("key", "string-value");

        var result = context.Get<int>("key");

        Assert.Equal(default, result);
    }

    [Fact]
    public void Items_IsCaseSensitive()
    {
        var context = new AxisPipelineContext();
        context.Set("Key", "upper");
        context.Set("key", "lower");

        Assert.Equal("upper", context.Get<string>("Key"));
        Assert.Equal("lower", context.Get<string>("key"));
        Assert.Equal(2, context.Items.Count);
    }

    [Fact]
    public void Set_OverwritesExistingValue()
    {
        var context = new AxisPipelineContext();
        context.Set("key", 1);
        context.Set("key", 2);

        Assert.Equal(2, context.Get<int>("key"));
    }

    [Fact]
    public void Get_NullValue_ReturnsDefault()
    {
        var context = new AxisPipelineContext();
        context.Items["key"] = null;

        var result = context.Get<string>("key");

        Assert.Null(result);
    }

    [Fact]
    public void Items_IsAccessibleDirectly()
    {
        var context = new AxisPipelineContext();
        context.Set("a", 1);
        context.Set("b", 2);

        Assert.Equal(2, context.Items.Count);
        Assert.True(context.Items.ContainsKey("a"));
        Assert.True(context.Items.ContainsKey("b"));
    }
}
