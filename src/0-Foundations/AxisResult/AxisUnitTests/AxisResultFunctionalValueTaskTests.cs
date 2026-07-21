namespace AxisUnitTests;

public class AxisResultFunctionalValueTaskTests
{
    private static readonly AxisError E1 = AxisError.NotFound("E1");
    private static readonly AxisError E2 = AxisError.ValidationRule("E2");

    private static ValueTask<T> VTAsync<T>(T v) => new(v);
    private static ValueTask<AxisResult> VTOkAsync() => new(AxisResult.Ok());
    private static ValueTask<AxisResult<T>> VTOkAsync<T>(T v) => new(AxisResult.Ok(v));
    private static ValueTask<AxisResult<T>> VTErrAsync<T>(AxisError e) => new(AxisResult.Error<T>(e));
    private static ValueTask<AxisResult> VTErrAsync(AxisError e) => new(AxisResult.Error(e));

    #region MapAsync

    [Fact]
    public async Task VT_MapAsync_NonGeneric_Success()
    {
        var r = await AxisResult.Ok().MapAsync(() => VTAsync(10));
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_MapAsync_NonGeneric_Failure_Propagates()
    {
        var src = AxisResult.Error(E1);
        var r = await src.MapAsync(() => VTAsync(10));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_MapAsync_Generic_Success()
    {
        var r = await AxisResult.Ok(5).MapAsync(x => VTAsync(x * 2));
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_MapAsync_Generic_Failure()
    {
        var r = await AxisResult.Error<int>(E1).MapAsync(x => VTAsync(x * 2));
        Assert.True(r.IsFailure);
    }

    #endregion

    #region ThenAsync

    [Fact]
    public async Task VT_ThenAsync_NonGeneric_Success_Chains()
    {
        var r = await AxisResult.Ok().ThenAsync(VTOkAsync);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_ThenAsync_NonGeneric_Success_To_Typed()
    {
        var r = await AxisResult.Ok().ThenAsync(() => VTOkAsync(8));
        Assert.Equal(8, r.Value);
    }

    [Fact]
    public async Task VT_ThenAsync_NonGeneric_Failure_ReturnsSelf()
    {
        var src = AxisResult.Error(E1);
        var r = await src.ThenAsync(VTOkAsync);
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_ThenAsync_NonGeneric_Failure_Typed_Propagates()
    {
        var src = AxisResult.Error(E1);
        var r = await src.ThenAsync(() => VTOkAsync(1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_ThenAsync_Generic_Success_NonGeneric_Preserves_Value()
    {
        var r = await AxisResult.Ok(5).ThenAsync(_ => VTOkAsync());
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_ThenAsync_Generic_Success_NonGeneric_Failure_Propagates_Error()
    {
        var r = await AxisResult.Ok(5).ThenAsync(_ => VTErrAsync(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_ThenAsync_Generic_Success_Typed()
    {
        var r = await AxisResult.Ok(5).ThenAsync(x => VTOkAsync(x.ToString()));
        Assert.Equal("5", r.Value);
    }

    [Fact]
    public async Task VT_ThenAsync_Generic_Failure_NonGeneric_Propagates()
    {
        var src = AxisResult.Error<int>(E1);
        var r = await src.ThenAsync(_ => VTOkAsync());
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_ThenAsync_Generic_Failure_Typed_Propagates()
    {
        var r = await AxisResult.Error<int>(E1).ThenAsync(x => VTOkAsync(x.ToString()));
        Assert.True(r.IsFailure);
    }

    #endregion

    #region TapAsync

    [Fact]
    public async Task VT_TapAsync_NonGeneric_Success_Runs()
    {
        var called = false;
        await AxisResult.Ok().TapAsync(() => { called = true; return ValueTask.CompletedTask; });
        Assert.True(called);
    }

    [Fact]
    public async Task VT_TapAsync_NonGeneric_Failure_Skips()
    {
        var called = false;
        await AxisResult.Error(E1).TapAsync(() => { called = true; return ValueTask.CompletedTask; });
        Assert.False(called);
    }

    [Fact]
    public async Task VT_TapAsync_Generic_NoValue_Success()
    {
        var called = false;
        var r = await AxisResult.Ok(5).TapAsync(() => { called = true; return ValueTask.CompletedTask; });
        Assert.True(called);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_TapAsync_Generic_NoValue_Failure_Skips()
    {
        var called = false;
        await AxisResult.Error<int>(E1).TapAsync(() => { called = true; return ValueTask.CompletedTask; });
        Assert.False(called);
    }

    [Fact]
    public async Task VT_TapAsync_Generic_WithValue_Success()
    {
        var captured = 0;
        await AxisResult.Ok(9).TapAsync(x => { captured = x; return ValueTask.CompletedTask; });
        Assert.Equal(9, captured);
    }

    [Fact]
    public async Task VT_TapAsync_Generic_WithValue_Failure_Skips()
    {
        var captured = 0;
        await AxisResult.Error<int>(E1).TapAsync(x => { captured = x; return ValueTask.CompletedTask; });
        Assert.Equal(0, captured);
    }

    #endregion

    #region TapErrorAsync

    [Fact]
    public async Task VT_TapErrorAsync_NonGeneric_Failure_Runs()
    {
        var captured = 0;
        await AxisResult.Error(E1).TapErrorAsync(errs => { captured = errs.Count; return ValueTask.CompletedTask; });
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_TapErrorAsync_NonGeneric_Success_Skips()
    {
        var called = false;
        await AxisResult.Ok().TapErrorAsync(_ => { called = true; return ValueTask.CompletedTask; });
        Assert.False(called);
    }

    [Fact]
    public async Task VT_TapErrorAsync_Generic_Failure_Runs()
    {
        var captured = 0;
        await AxisResult.Error<int>(E1).TapErrorAsync(errs => { captured = errs.Count; return ValueTask.CompletedTask; });
        Assert.Equal(1, captured);
    }

    [Fact]
    public async Task VT_TapErrorAsync_Generic_Success_Skips()
    {
        var called = false;
        await AxisResult.Ok(1).TapErrorAsync(_ => { called = true; return ValueTask.CompletedTask; });
        Assert.False(called);
    }

    #endregion

    #region MatchAsync

    [Fact]
    public async Task VT_MatchAsync_NonGeneric_Success()
    {
        var s = await AxisResult.Ok().MatchAsync(() => VTAsync("ok"), _ => VTAsync("no"));
        Assert.Equal("ok", s);
    }

    [Fact]
    public async Task VT_MatchAsync_NonGeneric_Failure()
    {
        var s = await AxisResult.Error(E1).MatchAsync(() => VTAsync("ok"), errs => VTAsync($"c{errs.Count}"));
        Assert.Equal("c1", s);
    }

    [Fact]
    public async Task VT_MatchAsync_Generic_Success()
    {
        var s = await AxisResult.Ok(10).MatchAsync(v => VTAsync(v.ToString()), _ => VTAsync("x"));
        Assert.Equal("10", s);
    }

    [Fact]
    public async Task VT_MatchAsync_Generic_Failure()
    {
        var s = await AxisResult.Error<int>(E1).MatchAsync(v => VTAsync(v.ToString()), _ => VTAsync("fail"));
        Assert.Equal("fail", s);
    }

    #endregion

    #region MapErrorAsync

    [Fact]
    public async Task VT_MapErrorAsync_NonGeneric_Failure_Maps()
    {
        var r = await AxisResult.Error(E1).MapErrorAsync(
            _ => new ValueTask<IEnumerable<AxisError>>(new[] { E2 }.AsEnumerable()));
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_MapErrorAsync_NonGeneric_Success_Skips()
    {
        var src = AxisResult.Ok();
        var r = await src.MapErrorAsync(_ => new ValueTask<IEnumerable<AxisError>>(new[] { E2 }.AsEnumerable()));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_MapErrorAsync_Generic_Failure_Maps()
    {
        var r = await AxisResult.Error<int>(E1).MapErrorAsync(
            _ => new ValueTask<IEnumerable<AxisError>>(new[] { E2 }.AsEnumerable()));
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_MapErrorAsync_Generic_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = await src.MapErrorAsync(_ => new ValueTask<IEnumerable<AxisError>>(new[] { E2 }.AsEnumerable()));
        Assert.Same(src, r);
    }

    #endregion

    #region OrElseAsync

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Success_Skips()
    {
        var src = AxisResult.Ok();
        var r = await src.OrElseAsync(_ => VTOkAsync());
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Failure_UsesFallback()
    {
        var r = await AxisResult.Error(E1).OrElseAsync(_ => VTOkAsync());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Combine_True_Concats()
    {
        var r = await AxisResult.Error(E1).OrElseAsync(_ => VTErrAsync(E2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Combine_True_Fallback_Success()
    {
        var r = await AxisResult.Error(E1).OrElseAsync(_ => VTOkAsync(), combineErrors: true);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Combine_True_Source_Success_Skips()
    {
        var src = AxisResult.Ok();
        var r = await src.OrElseAsync(_ => VTErrAsync(E2), combineErrors: true);
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_OrElseAsync_NonGeneric_Combine_False_Replaces()
    {
        var r = await AxisResult.Error(E1).OrElseAsync(_ => VTErrAsync(E2), combineErrors: false);
        Assert.Single(r.Errors);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Success_Skips()
    {
        var src = AxisResult.Ok(5);
        var r = await src.OrElseAsync(_ => VTOkAsync(99));
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Failure_UsesFallback()
    {
        var r = await AxisResult.Error<int>(E1).OrElseAsync(_ => VTOkAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Combine_True_Concats()
    {
        var r = await AxisResult.Error<int>(E1).OrElseAsync(_ => VTErrAsync<int>(E2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Combine_True_Fallback_Success()
    {
        var r = await AxisResult.Error<int>(E1).OrElseAsync(_ => VTOkAsync(7), combineErrors: true);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Combine_True_Source_Success_Skips()
    {
        var src = AxisResult.Ok(5);
        var r = await src.OrElseAsync(_ => VTOkAsync(9), combineErrors: true);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public async Task VT_OrElseAsync_Generic_Combine_False_Replaces()
    {
        var r = await AxisResult.Error<int>(E1).OrElseAsync(_ => VTErrAsync<int>(E2), combineErrors: false);
        Assert.Single(r.Errors);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region EnsureAsync

    [Fact]
    public async Task VT_EnsureAsync_Predicate_Success_Passes()
    {
        var r = await AxisResult.Ok(10).EnsureAsync(x => VTAsync(x > 0), E1);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_EnsureAsync_Predicate_Fails()
    {
        var r = await AxisResult.Ok(-1).EnsureAsync(x => VTAsync(x > 0), E1);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_EnsureAsync_Predicate_Already_Failure_Propagates()
    {
        var r = await AxisResult.Error<int>(E2).EnsureAsync(x => VTAsync(x > 0), E1);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_EnsureAsync_Validation_Success_Passes()
    {
        var r = await AxisResult.Ok(10).EnsureAsync(_ => VTOkAsync());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public async Task VT_EnsureAsync_Validation_Fails()
    {
        var r = await AxisResult.Ok(10).EnsureAsync(_ => VTErrAsync(E1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_EnsureAsync_Validation_Already_Failure_Skips()
    {
        var called = false;
        await AxisResult.Error<int>(E2).EnsureAsync(_ => { called = true; return VTOkAsync(); });
        Assert.False(called);
    }

    #endregion

    #region ThenUnlessAsync

    [Fact]
    public async Task VT_ThenUnlessAsync_PredicateTrue_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = await AxisResult.Ok(10).ThenUnlessAsync(x => x > 0, _ => { nextCalled = true; return VTOkAsync(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_ThenUnlessAsync_PredicateFalse_Next_Succeeds_Preserves_Value()
    {
        var nextCalled = false;
        var r = await AxisResult.Ok(10).ThenUnlessAsync(x => x < 0, _ => { nextCalled = true; return VTOkAsync(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task VT_ThenUnlessAsync_PredicateFalse_Next_Fails_Propagates_Error()
    {
        var r = await AxisResult.Ok(10).ThenUnlessAsync(x => x < 0, _ => VTErrAsync(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_ThenUnlessAsync_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = await AxisResult.Error<int>(E2).ThenUnlessAsync(
            _ => { predicateCalled = true; return true; },
            _ => { nextCalled = true; return VTOkAsync(); });
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    #endregion

    #region ThenWhenAsync

    [Fact]
    public async Task VT_ThenWhenAsync_PredicateFalse_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = await AxisResult.Ok(10).ThenWhenAsync(x => x < 0, x => { nextCalled = true; return VTOkAsync(x * 2); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task VT_ThenWhenAsync_PredicateTrue_Next_Succeeds_Replaces_Value()
    {
        var r = await AxisResult.Ok(10).ThenWhenAsync(x => x > 0, x => VTOkAsync(x * 2));
        Assert.True(r.IsSuccess);
        Assert.Equal(20, r.Value);
    }

    [Fact]
    public async Task VT_ThenWhenAsync_PredicateTrue_Next_Fails_Propagates_Error()
    {
        var r = await AxisResult.Ok(10).ThenWhenAsync(x => x > 0, _ => VTErrAsync<int>(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public async Task VT_ThenWhenAsync_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = await AxisResult.Error<int>(E2).ThenWhenAsync(
            _ => { predicateCalled = true; return true; },
            x => { nextCalled = true; return VTOkAsync(x); });
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    #endregion

    #region ZipAsync

    [Fact]
    public async Task VT_ZipAsync_PureMapper_Success()
    {
        var r = await AxisResult.Ok(5).ZipAsync(x => VTAsync(x * 2));
        Assert.Equal((5, 10), r.Value);
    }

    [Fact]
    public async Task VT_ZipAsync_PureMapper_Failure()
    {
        var r = await AxisResult.Error<int>(E1).ZipAsync(x => VTAsync(x * 2));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_ZipAsync_ResultMapper_Success()
    {
        var r = await AxisResult.Ok(5).ZipAsync(x => VTOkAsync(x + 1));
        Assert.Equal((5, 6), r.Value);
    }

    [Fact]
    public async Task VT_ZipAsync_ResultMapper_Fails()
    {
        var r = await AxisResult.Ok(5).ZipAsync(_ => VTErrAsync<int>(E1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_ZipAsync_ResultMapper_SourceFails_Skips()
    {
        var called = false;
        await AxisResult.Error<int>(E1).ZipAsync(x => { called = true; return VTOkAsync(x); });
        Assert.False(called);
    }

    #endregion

    #region RecoverAsync

    [Fact]
    public async Task VT_RecoverAsync_WithErrors_Failure()
    {
        var r = await AxisResult.Error<int>(E1).RecoverAsync(errs => VTAsync(errs.Count * 10));
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public async Task VT_RecoverAsync_WithErrors_Success_Skips()
    {
        var src = AxisResult.Ok(5);
        var r = await src.RecoverAsync(_ => VTAsync(99));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_RecoverAsync_NoArgs_Failure()
    {
        var r = await AxisResult.Error<int>(E1).RecoverAsync(() => VTAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_RecoverAsync_NoArgs_Success_Skips()
    {
        var src = AxisResult.Ok(5);
        var r = await src.RecoverAsync(() => VTAsync(99));
        Assert.Same(src, r);
    }

    #endregion

    #region RecoverWhenAsync

    [Fact]
    public async Task VT_RecoverWhenAsync_Predicate_Match()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync(errs => errs.Count == 1, _ => VTAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Predicate_NoMatch()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync(errs => errs.Count > 5, _ => VTAsync(99));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Predicate_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = await src.RecoverWhenAsync(_ => true, _ => VTAsync(9));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Type_Match()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync(AxisErrorType.NotFound, () => VTAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Type_NoMatch()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync(AxisErrorType.Conflict, () => VTAsync(99));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Type_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = await src.RecoverWhenAsync(AxisErrorType.NotFound, () => VTAsync(9));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Code_Match()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync("E1", () => VTAsync(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Code_NoMatch()
    {
        var r = await AxisResult.Error<int>(E1).RecoverWhenAsync("X", () => VTAsync(99));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_RecoverWhenAsync_Code_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = await src.RecoverWhenAsync("E1", () => VTAsync(9));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_RecoverNotFoundAsync_AllNotFound()
    {
        var r = await AxisResult.Error<int>([E1, AxisError.NotFound("X")]).RecoverNotFoundAsync(() => VTAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_RecoverNotFoundAsync_Mixed()
    {
        var r = await AxisResult.Error<int>([E1, E2]).RecoverNotFoundAsync(() => VTAsync(42));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_RecoverConflictAsync_AllConflict()
    {
        var r = await AxisResult.Error<int>([AxisError.Conflict("C1"), AxisError.Conflict("C2")]).RecoverConflictAsync(() => VTAsync(42));
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public async Task VT_RecoverConflictAsync_Mixed()
    {
        var r = await AxisResult.Error<int>([AxisError.Conflict("C1"), E2]).RecoverConflictAsync(() => VTAsync(42));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_RecoverNotFoundAsync_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = await src.RecoverNotFoundAsync(() => VTAsync(9));
        Assert.Same(src, r);
    }

    [Fact]
    public async Task VT_ElseNotFoundAsync_Success_MapsValue()
    {
        var r = await AxisResult.Ok(21).ElseNotFoundAsync(x => VTAsync($"value:{x}"), () => VTAsync("default"));
        Assert.Equal("value:21", r.Value);
    }

    [Fact]
    public async Task VT_ElseNotFoundAsync_AllNotFound_UsesRecovery()
    {
        var r = await AxisResult.Error<int>([E1, AxisError.NotFound("X")]).ElseNotFoundAsync(x => VTAsync($"value:{x}"), () => VTAsync("default"));
        Assert.Equal("default", r.Value);
    }

    [Fact]
    public async Task VT_ElseNotFoundAsync_OtherError_PropagatesErrors()
    {
        var r = await AxisResult.Error<int>(E2).ElseNotFoundAsync(x => VTAsync($"value:{x}"), () => VTAsync("default"));
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region SelectManyAsync

    [Fact]
    public async Task VT_SelectManyAsync_Success_Chains()
    {
        var r = await AxisResult.Ok(2).SelectManyAsync(
            x => VTOkAsync(x + 1),
            (x, y) => x * y);
        Assert.Equal(6, r.Value);
    }

    [Fact]
    public async Task VT_SelectManyAsync_Source_Fails()
    {
        var r = await AxisResult.Error<int>(E1).SelectManyAsync(
            x => VTOkAsync(x + 1),
            (x, y) => x * y);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task VT_SelectManyAsync_Binder_Fails()
    {
        var r = await AxisResult.Ok(2).SelectManyAsync(
            _ => VTErrAsync<int>(E2),
            (x, y) => x * y);
        Assert.True(r.IsFailure);
    }

    #endregion
}
