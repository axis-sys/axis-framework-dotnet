namespace AxisUnitTests;

public class AxisResultCancellationTests
{
    private static readonly AxisError E1 = AxisError.NotFound("NF");

    #region CancellationToken flows through the pipeline (Task)

    [Fact]
    public async Task ThenAsync_Task_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ThenAsync((v, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok(v + 1));
            }, cts.Token);

        Assert.Equal(2, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ToAxisResultAsync_Task_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ToAxisResultAsync((_, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task MapAsync_Task_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(5).AsTaskAsync()
            .MapAsync((v, ct) =>
            {
                observed = ct;
                return Task.FromResult(v * 2);
            }, cts.Token);

        Assert.Equal(10, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_Task_Forwards_CancellationToken()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsTaskAsync()
            .EnsureAsync((v, ct) =>
            {
                observed = ct;
                return Task.FromResult(v > 0);
            }, E1, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task Cancelled_Token_Before_Step_Triggers_OperationCanceled_In_Delegate()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await AxisResult.Ok(1).AsTaskAsync()
                .ThenAsync((v, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    return Task.FromResult(AxisResult.Ok(v + 1));
                }, cts.Token));
    }

    [Fact]
    public async Task ThenAsync_Task_Failure_Does_Not_Invoke_Delegate_Even_With_Token()
    {
        var invoked = false;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .ThenAsync((v, _) =>
            {
                invoked = true;
                return Task.FromResult(AxisResult.Ok(v + 1));
            }, cts.Token);

        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ZipAsync_Task_Forwards_Token_To_Failable_Mapper()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipAsync((v, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok(v + 1));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, 2), result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task TapAsync_Task_Forwards_CancellationToken_And_Preserves_Value()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok("tap").AsTaskAsync()
            .TapAsync((_, ct) =>
            {
                observed = ct;
                return Task.CompletedTask;
            }, cts.Token);

        Assert.Equal("tap", result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task TapAsync_Task_Failure_Does_Not_Invoke_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .TapAsync((_, _) => { invoked = true; return Task.CompletedTask; }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task EnsureAsync_Validation_Task_Success_Preserves_Value()
    {
        var observed = CancellationToken.None;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsTaskAsync()
            .EnsureAsync((_, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task EnsureAsync_Validation_Task_Failure_Propagates_Validation_Errors()
    {
        var result = await AxisResult.Ok(10).AsTaskAsync()
            .EnsureAsync((_, _) => Task.FromResult<AxisResult>(AxisError.ValidationRule("BAD")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("BAD", result.Errors[0].Code);
    }

    [Fact]
    public async Task EnsureAsync_Validation_Task_On_Failed_Upstream_Is_Skipped()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .EnsureAsync((_, _) => { invoked = true; return Task.FromResult(AxisResult.Ok()); }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_Task_Failure_Returns_Given_Error()
    {
        var result = await AxisResult.Ok(-1).AsTaskAsync()
            .EnsureAsync((v, _) => Task.FromResult(v > 0), E1, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(E1, result.Errors[0]);
    }

    [Fact]
    public async Task ThenUnlessAsync_Task_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsTaskAsync()
            .ThenUnlessAsync(v => v < 0, (_, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ThenUnlessAsync_Task_PredicateTrue_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Ok(10).AsTaskAsync()
            .ThenUnlessAsync(v => v > 0, (_, _) => { invoked = true; return Task.FromResult(AxisResult.Ok()); }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenWhenAsync_Task_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsTaskAsync()
            .ThenWhenAsync(v => v > 0, (v, ct) =>
            {
                observed = ct;
                return Task.FromResult(AxisResult.Ok(v * 2));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ThenWhenAsync_Task_PredicateFalse_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Ok(10).AsTaskAsync()
            .ThenWhenAsync(v => v < 0, (v, _) => { invoked = true; return Task.FromResult(AxisResult.Ok(v * 2)); }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.False(invoked);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_Task_On_Failed_Upstream_Is_Skipped()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .EnsureAsync((_, _) => { invoked = true; return Task.FromResult(true); }, E1, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ZipAsync_NonFailable_Task_Builds_Tuple_And_Forwards_Token()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipAsync((v, ct) =>
            {
                observed = ct;
                return Task.FromResult(v * 10);
            }, cts.Token);

        Assert.Equal((1, 10), result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ZipAsync_NonFailable_Task_Upstream_Failure_Propagates()
    {
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .ZipAsync((v, _) => Task.FromResult(v * 2), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ZipAsync_Failable_Task_Downstream_Failure_Propagates()
    {
        var result = await AxisResult.Ok(1).AsTaskAsync()
            .ZipAsync((_, _) => Task.FromResult(AxisResult.Error<int>(E1)), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ZipAsync_Failable_Task_Upstream_Failure_Is_Short_Circuited()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .ZipAsync((_, _) => { invoked = true; return Task.FromResult(AxisResult.Ok(2)); }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenAsync_Preserving_Task_Downstream_Failure_Propagates()
    {
        var result = await AxisResult.Ok("v").AsTaskAsync()
            .ThenAsync((_, _) => Task.FromResult<AxisResult>(AxisError.BusinessRule("BR")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("BR", result.Errors[0].Code);
    }

    [Fact]
    public async Task ThenAsync_Preserving_Task_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<string>(E1).AsTaskAsync()
            .ThenAsync((_, _) => { invoked = true; return Task.FromResult(AxisResult.Ok()); }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task MapAsync_Task_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsTaskAsync()
            .MapAsync((v, _) => { invoked = true; return Task.FromResult(v); }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    #endregion

    #region CancellationToken flows through the pipeline (ValueTask)

    [Fact]
    public async Task ThenAsync_ValueTask_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ThenAsync((v, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult<int>>(AxisResult.Ok(v + 1));
            }, cts.Token);

        Assert.Equal(2, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task MapAsync_ValueTask_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(3).AsValueTaskAsync()
            .MapAsync((v, ct) =>
            {
                observed = ct;
                return new ValueTask<int>(v + 10);
            }, cts.Token);

        Assert.Equal(13, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task MapAsync_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .MapAsync((v, _) => { invoked = true; return new ValueTask<int>(v + 1); }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenAsync_Failable_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .ThenAsync((v, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult<int>>(AxisResult.Ok(v + 1));
            }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenAsync_Preserving_ValueTask_Success_Preserves_Value()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok("kept").AsValueTaskAsync()
            .ThenAsync((_, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal("kept", result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ThenAsync_Preserving_ValueTask_Downstream_Failure_Propagates()
    {
        var result = await AxisResult.Ok("kept").AsValueTaskAsync()
            .ThenAsync((_, _) => new ValueTask<AxisResult>(AxisError.BusinessRule("BR")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("BR", result.Errors[0].Code);
    }

    [Fact]
    public async Task ThenAsync_Preserving_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<string>(E1).AsValueTaskAsync()
            .ThenAsync((_, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task TapAsync_ValueTask_Forwards_CancellationToken_And_Preserves_Value()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(42).AsValueTaskAsync()
            .TapAsync((_, ct) =>
            {
                observed = ct;
                return ValueTask.CompletedTask;
            }, cts.Token);

        Assert.Equal(42, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task TapAsync_ValueTask_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .TapAsync((_, _) => { invoked = true; return ValueTask.CompletedTask; }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_ValueTask_Success_Preserves_Value()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(5).AsValueTaskAsync()
            .EnsureAsync((v, ct) =>
            {
                observed = ct;
                return new ValueTask<bool>(v > 0);
            }, E1, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_ValueTask_Predicate_False_Returns_Error()
    {
        var result = await AxisResult.Ok(-1).AsValueTaskAsync()
            .EnsureAsync((v, _) => new ValueTask<bool>(v > 0), E1, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal(E1, result.Errors[0]);
    }

    [Fact]
    public async Task EnsureAsync_Predicate_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .EnsureAsync((_, _) => { invoked = true; return new ValueTask<bool>(true); }, E1, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task EnsureAsync_Validation_ValueTask_Success_Preserves_Value()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .EnsureAsync((_, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task EnsureAsync_Validation_ValueTask_Validation_Failure_Propagates()
    {
        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .EnsureAsync((_, _) =>
                new ValueTask<AxisResult>(AxisError.ValidationRule("BAD")), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("BAD", result.Errors[0].Code);
    }

    [Fact]
    public async Task EnsureAsync_Validation_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .EnsureAsync((_, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenUnlessAsync_ValueTask_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .ThenUnlessAsync(v => v < 0, (_, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ThenUnlessAsync_ValueTask_PredicateTrue_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .ThenUnlessAsync(v => v > 0, (_, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ThenWhenAsync_ValueTask_Forwards_CancellationToken_To_Delegate()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .ThenWhenAsync(v => v > 0, (v, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult<int>>(AxisResult.Ok(v * 2));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(20, result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ThenWhenAsync_ValueTask_PredicateFalse_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Ok(10).AsValueTaskAsync()
            .ThenWhenAsync(v => v < 0, (v, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult<int>>(AxisResult.Ok(v * 2));
            }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ZipAsync_NonFailable_ValueTask_Builds_Tuple_And_Forwards_Token()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(2).AsValueTaskAsync()
            .ZipAsync((v, ct) =>
            {
                observed = ct;
                return new ValueTask<int>(v * 5);
            }, cts.Token);

        Assert.Equal((2, 10), result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ZipAsync_NonFailable_ValueTask_Upstream_Failure_Propagates()
    {
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .ZipAsync((v, _) => new ValueTask<int>(v * 2), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ZipAsync_Failable_ValueTask_Success_Builds_Tuple()
    {
        var observed = CancellationToken.None;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ZipAsync((_, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult<string>>(AxisResult.Ok("hi"));
            }, cts.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal((1, "hi"), result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ZipAsync_Failable_ValueTask_Downstream_Failure_Propagates()
    {
        var result = await AxisResult.Ok(1).AsValueTaskAsync()
            .ZipAsync((_, _) =>
                new ValueTask<AxisResult<int>>(AxisResult.Error<int>(E1)), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ZipAsync_Failable_ValueTask_Upstream_Failure_Skips_Delegate()
    {
        var invoked = false;
        var result = await AxisResult.Error<int>(E1).AsValueTaskAsync()
            .ZipAsync((_, _) =>
            {
                invoked = true;
                return new ValueTask<AxisResult<int>>(AxisResult.Ok(2));
            }, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.False(invoked);
    }

    [Fact]
    public async Task ActionAsync_ValueTask_Preserves_Value_And_Forwards_Token()
    {
        CancellationToken observed = default;
        using var cts = new CancellationTokenSource();

        var result = await AxisResult.Ok("kept").AsValueTaskAsync()
            .ActionAsync((_, ct) =>
            {
                observed = ct;
                return new ValueTask<AxisResult>(AxisResult.Ok());
            }, cts.Token);

        Assert.Equal("kept", result.Value);
        Assert.Equal(cts.Token, observed);
    }

    [Fact]
    public async Task ActionAsync_ValueTask_Downstream_Failure_Propagates()
    {
        var result = await AxisResult.Ok("kept").AsValueTaskAsync()
            .ActionAsync((_, _) =>
                new ValueTask<AxisResult>(AxisError.Conflict("C")), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    #endregion
}
