namespace AxisUnitTests;

public class AxisResultExtensionsValueTaskTests
{
    private static readonly AxisError _e1 = AxisError.NotFound("E1");
    private static readonly AxisError _e2 = AxisError.ValidationRule("E2");

    private static ValueTask<T> VtAsync<T>(T v) => new(v);
    private static ValueTask<AxisResult> VtOkAsync() => new(AxisResult.Ok());
    private static ValueTask<AxisResult<T>> VtOkAsync<T>(T v) => new(AxisResult.Ok(v));
    private static ValueTask<AxisResult> VtErrAsync(AxisError e) => new(AxisResult.Error(e));
    private static ValueTask<AxisResult<T>> VtErrAsync<T>(AxisError e) => new(AxisResult.Error<T>(e));

    #region AsValueTask

    [Fact]
    public async Task AsValueTask_NonGeneric()
    {
        var vt = AxisResult.Ok().AsValueTaskAsync();
        var r = await vt;
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task AsValueTask_Generic()
    {
        var vt = AxisResult.Ok(5).AsValueTaskAsync();
        var r = await vt;
        Assert.Equal(5, r.Value);
    }

    #endregion

    #region NonGeneric ValueTask extensions

    [Fact]
    public async Task VT_Ext_ThenAsync_Sync_NG()
    {
        var r = await VtOkAsync().ThenAsync(AxisResult.Ok);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Sync_Typed()
    {
        var r = await VtOkAsync().ThenAsync(() => AxisResult.Ok(5));
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Async_NG()
    {
        var r = await VtOkAsync().ThenAsync(VtOkAsync);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Async_Typed()
    {
        var r = await VtOkAsync().ThenAsync(() => VtOkAsync(7));
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public async Task VT_Ext_TapAsync_Sync()
    {
        var called = false;
        await VtOkAsync().TapAsync(() => called = true);
        Assert.True(called);
    }

    [Fact]
    public async Task VT_Ext_TapAsync_Async()
    {
        var called = false;
        await VtOkAsync().TapAsync(() => { called = true; return ValueTask.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task VT_Ext_TapErrorAsync_Sync()
    {
        var captured = 0;
        await VtErrAsync(_e1).TapErrorAsync(errs => captured = errs.Count);
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_Ext_TapErrorAsync_Async()
    {
        var captured = 0;
        await VtErrAsync(_e1).TapErrorAsync(errs => { captured = errs.Count; return ValueTask.CompletedTask; });
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Sync_Success()
    {
        var s = await VtOkAsync().MatchAsync(() => "ok", _ => "no");
        Assert.Equal("ok", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Async_Success()
    {
        var s = await VtOkAsync().MatchAsync(() => VtAsync("ok"), _ => VtAsync("no"));
        Assert.Equal("ok", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Sync_Failure()
    {
        var s = await VtErrAsync(_e1).MatchAsync(() => "ok", errs => $"c{errs.Count}");
        Assert.Equal("c1", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Async_Failure()
    {
        var s = await VtErrAsync(_e1).MatchAsync(() => VtAsync("ok"), errs => VtAsync($"c{errs.Count}"));
        Assert.Equal("c1", s);
    }

    [Fact]
    public async Task VT_Ext_WithValueAsync_Success_ReturnsNewValue()
    {
        var r = await VtOkAsync().WithValueAsync(42);
        Assert.True(r.IsSuccess);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_WithValueAsync_Failure_PropagatesErrors()
    {
        var r = await VtErrAsync(_e1).WithValueAsync(42);
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    #endregion

    #region Generic ValueTask extensions

    [Fact]
    public async Task VT_Ext_ActionAsync_Success_Preserves_Value()
    {
        var r = await VtOkAsync(5).ActionAsync(_ => VtOkAsync());
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ActionAsync_Success_Failure_Propagates_Error()
    {
        var r = await VtOkAsync(5).ActionAsync(_ => VtErrAsync(_e1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_ActionAsync_Source_Failure_Skips()
    {
        var called = false;
        var r = await VtErrAsync<int>(_e1).ActionAsync(_ => { called = true; return VtOkAsync(); });
        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_MapAsync_Sync()
    {
        var r = await VtOkAsync(5).MapAsync(x => x * 2);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_Ext_MapAsync_Async()
    {
        var r = await VtOkAsync(5).MapAsync(x => VtAsync(x * 2));
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Sync_NG_Preserves_Value()
    {
        var r = await VtOkAsync(5).ThenAsync(_ => AxisResult.Ok());
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Sync_NG_Failure_Propagates()
    {
        var r = await VtOkAsync(5).ThenAsync(_ => AxisResult.Error(_e1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Sync_Typed()
    {
        var r = await VtOkAsync(5).ThenAsync(x => AxisResult.Ok(x.ToString()));
        Assert.Equal("5", r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Async_NG_Preserves_Value()
    {
        var r = await VtOkAsync(5).ThenAsync(_ => VtOkAsync());
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Async_NG_Failure_Propagates()
    {
        var r = await VtOkAsync(5).ThenAsync(_ => VtErrAsync(_e1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_ThenAsync_Generic_Async_Typed()
    {
        var r = await VtOkAsync(5).ThenAsync(x => VtOkAsync(x.ToString()));
        Assert.Equal("5", r.Value);
    }

    [Fact]
    public async Task VT_Ext_TapAsync_Generic_Sync()
    {
        var captured = 0;
        await VtOkAsync(9).TapAsync(x => captured = x);
        Assert.Equal(9, captured);
    }

    [Fact]
    public async Task VT_Ext_TapAsync_Generic_Async()
    {
        var captured = 0;
        await VtOkAsync(9).TapAsync(x => { captured = x; return ValueTask.CompletedTask; });
        Assert.Equal(9, captured);
    }

    [Fact]
    public async Task VT_Ext_TapErrorAsync_Generic_Sync()
    {
        var captured = 0;
        await VtErrAsync<int>(_e1).TapErrorAsync(errs => captured = errs.Count);
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_Ext_TapErrorAsync_Generic_Async()
    {
        var captured = 0;
        await VtErrAsync<int>(_e1).TapErrorAsync(errs => { captured = errs.Count; return ValueTask.CompletedTask; });
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Predicate_Sync()
    {
        var r = await VtOkAsync(10).EnsureAsync(x => x > 0, _e1);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Predicate_Async()
    {
        var r = await VtOkAsync(10).EnsureAsync(x => VtAsync(x > 0), _e1);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Validation_Sync()
    {
        var r = await VtOkAsync(10).EnsureAsync(_ => AxisResult.Ok());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Validation_Async()
    {
        var r = await VtOkAsync(10).EnsureAsync(_ => VtOkAsync());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Predicate_Sync_Fail()
    {
        var r = await VtOkAsync(-1).EnsureAsync(x => x > 0, _e1);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Predicate_Async_Fail()
    {
        var r = await VtOkAsync(-1).EnsureAsync(x => VtAsync(x > 0), _e1);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Validation_Sync_Fail()
    {
        var r = await VtOkAsync(10).EnsureAsync(_ => AxisResult.Error(_e1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_EnsureAsync_Validation_Async_Fail()
    {
        var r = await VtOkAsync(10).EnsureAsync(_ => VtErrAsync(_e1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextSync_PredicateTrue_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = await VtOkAsync(10).ThenUnlessAsync(x => x > 0, _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextSync_PredicateFalse_Next_Succeeds_Preserves_Value()
    {
        var nextCalled = false;
        var r = await VtOkAsync(10).ThenUnlessAsync(x => x < 0, _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextSync_PredicateFalse_Next_Fails_Propagates_Error()
    {
        var r = await VtOkAsync(10).ThenUnlessAsync(x => x < 0, _ => AxisResult.Error(_e1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextSync_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = await VtErrAsync<int>(_e1).ThenUnlessAsync(
            _ => { predicateCalled = true; return true; },
            _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsFailure);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextAsync_PredicateFalse_Next_Succeeds_Preserves_Value()
    {
        var nextCalled = false;
        var r = await VtOkAsync(10).ThenUnlessAsync(x => x < 0, _ => { nextCalled = true; return VtOkAsync(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenUnlessAsync_NextAsync_PredicateFalse_Next_Fails_Propagates_Error()
    {
        var r = await VtOkAsync(10).ThenUnlessAsync(x => x < 0, _ => VtErrAsync(_e1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_ThenWhenAsync_NextSync_PredicateFalse_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = await VtOkAsync(10).ThenWhenAsync(x => x < 0, x => { nextCalled = true; return AxisResult.Ok(x * 2); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenWhenAsync_NextSync_PredicateTrue_Next_Succeeds_Replaces_Value()
    {
        var r = await VtOkAsync(10).ThenWhenAsync(x => x > 0, x => AxisResult.Ok(x * 2));
        Assert.True(r.IsSuccess);
        Assert.Equal(20, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenWhenAsync_NextSync_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = await VtErrAsync<int>(_e1).ThenWhenAsync(
            _ => { predicateCalled = true; return true; },
            x => { nextCalled = true; return AxisResult.Ok(x); });
        Assert.True(r.IsFailure);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_Ext_ThenWhenAsync_NextAsync_PredicateTrue_Next_Succeeds_Replaces_Value()
    {
        var r = await VtOkAsync(10).ThenWhenAsync(x => x > 0, x => VtOkAsync(x * 2));
        Assert.True(r.IsSuccess);
        Assert.Equal(20, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ThenWhenAsync_NextAsync_PredicateTrue_Next_Fails_Propagates_Error()
    {
        var r = await VtOkAsync(10).ThenWhenAsync(x => x > 0, _ => VtErrAsync<int>(_e1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Ext_ZipAsync_PureSync()
    {
        var r = await VtOkAsync(5).ZipAsync(x => x * 2);
        Assert.Equal((5, 10), r.Value);
    }

    [Fact]
    public async Task VT_Ext_ZipAsync_PureAsync()
    {
        var r = await VtOkAsync(5).ZipAsync(x => VtAsync(x * 2));
        Assert.Equal((5, 10), r.Value);
    }

    [Fact]
    public async Task VT_Ext_ZipAsync_ResultSync()
    {
        var r = await VtOkAsync(5).ZipAsync(x => AxisResult.Ok(x + 1));
        Assert.Equal((5, 6), r.Value);
    }

    [Fact]
    public async Task VT_Ext_ZipAsync_ResultAsync()
    {
        var r = await VtOkAsync(5).ZipAsync(x => VtOkAsync(x + 1));
        Assert.Equal((5, 6), r.Value);
    }

    [Fact]
    public async Task VT_Ext_MapErrorAsync_List_Sync()
    {
        var r = await VtErrAsync<int>(_e1).MapErrorAsync(_ => new[] { _e2 }.AsEnumerable());
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_MapErrorAsync_Item_Sync()
    {
        var r = await VtErrAsync<int>(_e1).MapErrorAsync(e => AxisError.Mapping(e.Code));
        Assert.Equal(AxisErrorType.Mapping, r.Errors[0].Type);
    }

    [Fact]
    public async Task VT_Ext_MapErrorAsync_Async()
    {
        var r = await VtErrAsync<int>(_e1).MapErrorAsync(_ => new ValueTask<IEnumerable<AxisError>>(new[] { _e2 }.AsEnumerable()));
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_RecoverAsync_WithErrors_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverAsync(errs => errs.Count * 10);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverAsync_WithErrors_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverAsync(errs => VtAsync(errs.Count * 10));
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverAsync_NoArgs_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverAsync(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverAsync_NoArgs_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverAsync(() => VtAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverAsync_Default()
    {
        var r = await VtErrAsync<int>(_e1).RecoverAsync(7);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Predicate_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync(_ => true, _ => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Predicate_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync(_ => true, _ => VtAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Type_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync(AxisErrorType.NotFound, () => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Type_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync(AxisErrorType.NotFound, () => VtAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Code_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync("E1", () => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverWhenAsync_Code_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverWhenAsync("E1", () => VtAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverConflictAsync_Sync()
    {
        var r = await VtErrAsync<int>(AxisError.Conflict("C1")).RecoverConflictAsync(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverConflictAsync_Async()
    {
        var r = await VtErrAsync<int>(AxisError.Conflict("C1")).RecoverConflictAsync(() => VtAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverNotFoundAsync_Sync()
    {
        var r = await VtErrAsync<int>(_e1).RecoverNotFoundAsync(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_RecoverNotFoundAsync_Async()
    {
        var r = await VtErrAsync<int>(_e1).RecoverNotFoundAsync(() => VtAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Sync_Success_MapsValue()
    {
        var r = await VtOkAsync(21).ElseNotFoundAsync(x => $"value:{x}", () => "default");
        Assert.Equal("value:21", r.Value);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Sync_AllNotFound_UsesRecovery()
    {
        var r = await VtErrAsync<int>(_e1).ElseNotFoundAsync(x => $"value:{x}", () => "default");
        Assert.Equal("default", r.Value);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Sync_OtherError_PropagatesErrors()
    {
        var r = await VtErrAsync<int>(_e2).ElseNotFoundAsync(x => $"value:{x}", () => "default");
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Async_Success_MapsValue()
    {
        var r = await VtOkAsync(21).ElseNotFoundAsync(x => VtAsync($"value:{x}"), () => VtAsync("default"));
        Assert.Equal("value:21", r.Value);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Async_AllNotFound_UsesRecovery()
    {
        var r = await VtErrAsync<int>(_e1).ElseNotFoundAsync(x => VtAsync($"value:{x}"), () => VtAsync("default"));
        Assert.Equal("default", r.Value);
    }

    [Fact]
    public async Task VT_Ext_ElseNotFoundAsync_Async_OtherError_PropagatesErrors()
    {
        var r = await VtErrAsync<int>(_e2).ElseNotFoundAsync(x => VtAsync($"value:{x}"), () => VtAsync("default"));
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_Ext_OrElseAsync_Sync()
    {
        var r = await VtErrAsync<int>(_e1).OrElseAsync(_ => AxisResult.Ok(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_OrElseAsync_Async()
    {
        var r = await VtErrAsync<int>(_e1).OrElseAsync(_ => VtOkAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_Ext_OrElseAsync_Sync_Combine()
    {
        var r = await VtErrAsync<int>(_e1).OrElseAsync(_ => AxisResult.Error<int>(_e2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public async Task VT_Ext_OrElseAsync_Async_Combine()
    {
        var r = await VtErrAsync<int>(_e1).OrElseAsync(_ => VtErrAsync<int>(_e2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Generic_Sync_Success()
    {
        var s = await VtOkAsync(10).MatchAsync(v => v.ToString(), _ => "x");
        Assert.Equal("10", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Generic_Async_Success()
    {
        var s = await VtOkAsync(10).MatchAsync(v => VtAsync(v.ToString()), _ => VtAsync("x"));
        Assert.Equal("10", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Generic_Sync_Failure()
    {
        var s = await VtErrAsync<int>(_e1).MatchAsync(v => v.ToString(), _ => "fail");
        Assert.Equal("fail", s);
    }

    [Fact]
    public async Task VT_Ext_MatchAsync_Generic_Async_Failure()
    {
        var s = await VtErrAsync<int>(_e1).MatchAsync(v => VtAsync(v.ToString()), _ => VtAsync("fail"));
        Assert.Equal("fail", s);
    }

    [Fact]
    public async Task VT_Ext_SelectManyAsync_Sync()
    {
        var r = await VtOkAsync(2).SelectManyAsync(x => AxisResult.Ok(x + 1), (x, y) => x * y);
        Assert.Equal(6, r.Value);
    }

    [Fact]
    public async Task VT_Ext_SelectManyAsync_Async()
    {
        var r = await VtOkAsync(2).SelectManyAsync(x => VtOkAsync(x + 1), (x, y) => x * y);
        Assert.Equal(6, r.Value);
    }

    #endregion

    #region Tuple2

    [Fact]
    public async Task VT_Tuple2_MapAsync_Sync()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.MapAsync((a, b) => a + b);
        Assert.Equal(3, r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_MapAsync_Async()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.MapAsync((a, b) => VtAsync(a + b));
        Assert.Equal(3, r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_Sync()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((a, b) => a + b);
        Assert.Equal((1, 2, 3), r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_Async()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((a, b) => VtAsync(a + b));
        Assert.Equal((1, 2, 3), r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultSync()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((a, b) => AxisResult.Ok(a + b));
        Assert.Equal((1, 2, 3), r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultAsync()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((a, b) => VtOkAsync(a + b));
        Assert.Equal((1, 2, 3), r.Value);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_Sync_Fail()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Error<(int, int)>(_e1));
        var r = await vt.ZipAsync((a, b) => a + b);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_Async_Fail()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Error<(int, int)>(_e1));
        var r = await vt.ZipAsync((a, b) => VtAsync(a + b));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultSync_Fail_Source()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Error<(int, int)>(_e1));
        var r = await vt.ZipAsync((a, b) => AxisResult.Ok(a + b));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultSync_Fail_Mapper()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((_, _) => AxisResult.Error<int>(_e2));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultAsync_Fail_Source()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Error<(int, int)>(_e1));
        var r = await vt.ZipAsync((a, b) => VtOkAsync(a + b));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple2_ZipAsync_ResultAsync_Fail_Mapper()
    {
        var vt = new ValueTask<AxisResult<(int, int)>>(AxisResult.Ok((1, 2)));
        var r = await vt.ZipAsync((_, _) => VtErrAsync<int>(_e2));
        Assert.True(r.IsFailure);
    }

    #endregion

    #region Tuple3

    [Fact]
    public async Task VT_Tuple3_MapAsync_Sync()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.MapAsync((a, b, c) => a + b + c);
        Assert.Equal(6, r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_MapAsync_Async()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.MapAsync((a, b, c) => VtAsync(a + b + c));
        Assert.Equal(6, r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_Sync()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((a, b, c) => a + b + c);
        Assert.Equal((1, 2, 3, 6), r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_Async()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((a, b, c) => VtAsync(a + b + c));
        Assert.Equal((1, 2, 3, 6), r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultSync()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((a, b, c) => AxisResult.Ok(a + b + c));
        Assert.Equal((1, 2, 3, 6), r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultAsync()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((a, b, c) => VtOkAsync(a + b + c));
        Assert.Equal((1, 2, 3, 6), r.Value);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_Sync_Fail()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Error<(int, int, int)>(_e1));
        var r = await vt.ZipAsync((a, b, c) => a + b + c);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_Async_Fail()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Error<(int, int, int)>(_e1));
        var r = await vt.ZipAsync((a, b, c) => VtAsync(a + b + c));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultSync_Fail_Source()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Error<(int, int, int)>(_e1));
        var r = await vt.ZipAsync((a, b, c) => AxisResult.Ok(a + b + c));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultSync_Fail_Mapper()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((_, _, _) => AxisResult.Error<int>(_e2));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultAsync_Fail_Source()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Error<(int, int, int)>(_e1));
        var r = await vt.ZipAsync((a, b, c) => VtOkAsync(a + b + c));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_Tuple3_ZipAsync_ResultAsync_Fail_Mapper()
    {
        var vt = new ValueTask<AxisResult<(int, int, int)>>(AxisResult.Ok((1, 2, 3)));
        var r = await vt.ZipAsync((_, _, _) => VtErrAsync<int>(_e2));
        Assert.True(r.IsFailure);
    }

    #endregion

    #region Tuple4

    [Fact]
    public async Task VT_Tuple4_MapAsync_Sync()
    {
        var vt = new ValueTask<AxisResult<(int, int, int, int)>>(AxisResult.Ok((1, 2, 3, 4)));
        var r = await vt.MapAsync((a, b, c, d) => a + b + c + d);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_Tuple4_MapAsync_Async()
    {
        var vt = new ValueTask<AxisResult<(int, int, int, int)>>(AxisResult.Ok((1, 2, 3, 4)));
        var r = await vt.MapAsync((a, b, c, d) => VtAsync(a + b + c + d));
        Assert.Equal(10, r.Value);
    }

    #endregion

    #region RequireNotFoundAsync (NonGeneric)

    [Fact]
    public async Task VT_RequireNotFoundAsync_NG_Success_ReturnsError()
    {
        var r = await VtOkAsync().RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "ALREADY_EXISTS");
    }

    [Fact]
    public async Task VT_RequireNotFoundAsync_NG_NotFoundError_ReturnsOk()
    {
        var notFound = AxisError.NotFound("NOT_FOUND");
        var r = await VtErrAsync(notFound).RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_RequireNotFoundAsync_NG_OtherError_PropagatesOriginal()
    {
        var r = await VtErrAsync(_e2).RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "E2");
    }

    #endregion

    #region RequireNotFoundAsync (Generic)

    [Fact]
    public async Task VT_RequireNotFoundAsync_Generic_Success_ReturnsError()
    {
        var r = await VtOkAsync(5).RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "ALREADY_EXISTS");
    }

    [Fact]
    public async Task VT_RequireNotFoundAsync_Generic_NotFoundError_ReturnsOk()
    {
        var notFound = AxisError.NotFound("NOT_FOUND");
        var r = await VtErrAsync<int>(notFound).RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_RequireNotFoundAsync_Generic_OtherError_PropagatesOriginal()
    {
        var r = await VtErrAsync<int>(_e2).RequireNotFoundAsync(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "E2");
    }

    #endregion
}
