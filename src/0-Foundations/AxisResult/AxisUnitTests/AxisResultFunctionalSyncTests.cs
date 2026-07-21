namespace AxisUnitTests;

public class AxisResultFunctionalSyncTests
{
    private static readonly AxisError E1 = AxisError.NotFound("E1");
    private static readonly AxisError E2 = AxisError.ValidationRule("E2");
    private static readonly AxisError E3 = AxisError.Conflict("E3");

    #region Map (non-generic)

    [Fact]
    public void Map_NonGeneric_Success_Invokes_Mapper()
    {
        var r = AxisResult.Ok().Map(() => 10);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public void Map_NonGeneric_Failure_Propagates()
    {
        var src = AxisResult.Error(E1);
        var r = src.Map(() => 10);
        Assert.True(r.IsFailure);
        Assert.Single(r.Errors);
    }

    #endregion

    #region Map (generic)

    [Fact]
    public void Map_Generic_Success_Transforms_Value()
    {
        var r = AxisResult.Ok(5).Map(x => x * 2);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public void Map_Generic_Failure_Propagates()
    {
        var r = AxisResult.Error<int>(E1).Map(x => x * 2);
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    #endregion

    #region Then (non-generic)

    [Fact]
    public void Then_NonGeneric_Success_Chains()
    {
        var r = AxisResult.Ok().Then(AxisResult.Ok);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Then_NonGeneric_Success_Chains_To_Typed()
    {
        var r = AxisResult.Ok().Then(() => AxisResult.Ok(7));
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public void Then_NonGeneric_Failure_Skips_Next()
    {
        var called = false;
        var src = AxisResult.Error(E1);
        var r = src.Then(() => { called = true; return AxisResult.Ok(); });
        Assert.True(r.IsFailure);
        Assert.False(called);
    }

    [Fact]
    public void Then_NonGeneric_Failure_Typed_Skips_Next()
    {
        var called = false;
        var src = AxisResult.Error(E1);
        var r = src.Then(() => { called = true; return AxisResult.Ok(1); });
        Assert.True(r.IsFailure);
        Assert.False(called);
    }

    #endregion

    #region Then (generic)

    [Fact]
    public void Then_Generic_Success_To_NonGeneric_Preserves_Value()
    {
        var r = AxisResult.Ok(5).Then(_ => AxisResult.Ok());
        Assert.True(r.IsSuccess);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public void Then_Generic_Success_To_NonGeneric_Failure_Propagates_Error()
    {
        var r = AxisResult.Ok(5).Then(_ => AxisResult.Error(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void Then_Generic_Success_To_Typed()
    {
        var r = AxisResult.Ok(5).Then(x => AxisResult.Ok(x.ToString()));
        Assert.Equal("5", r.Value);
    }

    [Fact]
    public void Then_Generic_Failure_To_NonGeneric_Propagates()
    {
        var src = AxisResult.Error<int>(E1);
        var r = src.Then(_ => AxisResult.Ok());
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void Then_Generic_Failure_To_Typed_Propagates()
    {
        var r = AxisResult.Error<int>(E1).Then(x => AxisResult.Ok(x.ToString()));
        Assert.True(r.IsFailure);
    }

    #endregion

    #region ToAxisResult

    [Fact]
    public void ToAxisResult_Generic_Success_Invokes()
    {
        var called = false;
        var r = AxisResult.Ok(5).ToAxisResult(_ => { called = true; return AxisResult.Ok(); });
        Assert.True(called);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void ToAxisResult_Generic_Failure_Skips()
    {
        var called = false;
        var r = AxisResult.Error<int>(E1).ToAxisResult(_ => { called = true; return AxisResult.Ok(); });
        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void ToAxisResult_Parameterless_Generic_Success_ReturnsOk()
    {
        var r = AxisResult.Ok(5).ToAxisResult();
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void ToAxisResult_Parameterless_Generic_Failure_PropagatesErrors()
    {
        var r = AxisResult.Error<int>(E1).ToAxisResult();
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "E1");
    }

    #endregion

    #region Tap / TapError

    [Fact]
    public void Tap_NonGeneric_Success_Invokes()
    {
        var called = false;
        var r = AxisResult.Ok().Tap(() => called = true);
        Assert.True(called);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Tap_NonGeneric_Failure_Skips()
    {
        var called = false;
        var src = AxisResult.Error(E1);
        var r = src.Tap(() => called = true);
        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Tap_Parameterless_On_Generic_Returns_Typed()
    {
        var called = false;
        var r = AxisResult.Ok(5).Tap(() => called = true);
        Assert.True(called);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public void Tap_Parameterless_On_Generic_Failure_Skips()
    {
        var called = false;
        var src = AxisResult.Error<int>(E1);
        src.Tap(() => called = true);
        Assert.False(called);
    }

    [Fact]
    public void Tap_With_Value_Success_Invokes()
    {
        var captured = 0;
        var r = AxisResult.Ok(7).Tap(x => captured = x);
        Assert.Equal(7, captured);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public void Tap_With_Value_Failure_Skips()
    {
        var captured = 0;
        var src = AxisResult.Error<int>(E1);
        src.Tap(x => captured = x);
        Assert.Equal(0, captured);
    }

    [Fact]
    public void TapError_NonGeneric_Failure_Invokes()
    {
        var captured = 0;
        AxisResult.Error(E1).TapError(errs => captured = errs.Count);
        Assert.Equal(1, captured);
    }

    [Fact]
    public void TapError_NonGeneric_Success_Skips()
    {
        var called = false;
        AxisResult.Ok().TapError(_ => called = true);
        Assert.False(called);
    }

    [Fact]
    public void TapError_Generic_Failure_Invokes()
    {
        var captured = 0;
        AxisResult.Error<int>(E1).TapError(errs => captured = errs.Count);
        Assert.Equal(1, captured);
    }

    [Fact]
    public void TapError_Generic_Success_Skips()
    {
        var called = false;
        AxisResult.Ok(1).TapError(_ => called = true);
        Assert.False(called);
    }

    #endregion

    #region Match

    [Fact]
    public void Match_NonGeneric_Success_Path()
    {
        var s = AxisResult.Ok().Match(() => "ok", _ => "fail");
        Assert.Equal("ok", s);
    }

    [Fact]
    public void Match_NonGeneric_Failure_Path()
    {
        var s = AxisResult.Error(E1).Match(() => "ok", errs => $"fail:{errs.Count}");
        Assert.Equal("fail:1", s);
    }

    [Fact]
    public void Match_Generic_Success_Path()
    {
        var s = AxisResult.Ok(42).Match(v => v.ToString(), _ => "x");
        Assert.Equal("42", s);
    }

    [Fact]
    public void Match_Generic_Failure_Path()
    {
        var s = AxisResult.Error<int>(E1).Match(v => v.ToString(), errs => $"c={errs.Count}");
        Assert.Equal("c=1", s);
    }

    #endregion

    #region MapError

    [Fact]
    public void MapError_NonGeneric_List_Success_Returns_This()
    {
        var src = AxisResult.Ok();
        var r = src.MapError(errs => errs);
        Assert.Same(src, r);
    }

    [Fact]
    public void MapError_NonGeneric_List_Failure_Maps()
    {
        var r = AxisResult.Error(E1).MapError(errs => errs.Concat([E2]));
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void MapError_NonGeneric_Item_Maps_Each()
    {
        var r = AxisResult.Error(new[] { E1, E2 }).MapError(e => AxisError.Mapping("M_" + e.Code));
        Assert.Equal(2, r.Errors.Count);
        Assert.Equal("M_E1", r.Errors[0].Code);
        Assert.Equal("M_E2", r.Errors[1].Code);
        Assert.All(r.Errors, e => Assert.Equal(AxisErrorType.Mapping, e.Type));
    }

    [Fact]
    public void MapError_NonGeneric_Item_Success_Is_Noop()
    {
        var src = AxisResult.Ok();
        var r = src.MapError(e => e);
        Assert.Same(src, r);
    }

    [Fact]
    public void MapError_Generic_List_Failure_Maps()
    {
        var r = AxisResult.Error<int>(E1).MapError(_ => [E3]);
        Assert.Equal("E3", r.Errors[0].Code);
    }

    [Fact]
    public void MapError_Generic_List_Success_Returns_This()
    {
        var src = AxisResult.Ok(1);
        var r = src.MapError(errs => errs);
        Assert.Same(src, r);
    }

    [Fact]
    public void MapError_Generic_Item_Maps_Each()
    {
        var r = AxisResult.Error<int>([E1, E2]).MapError(e => AxisError.Mapping(e.Code));
        Assert.All(r.Errors, e => Assert.Equal(AxisErrorType.Mapping, e.Type));
    }

    #endregion

    #region OrElse (non-generic)

    [Fact]
    public void OrElse_NonGeneric_Success_Skips_Fallback()
    {
        var src = AxisResult.Ok();
        var called = false;
        var r = src.OrElse(_ => { called = true; return AxisResult.Ok(); });
        Assert.False(called);
        Assert.Same(src, r);
    }

    [Fact]
    public void OrElse_NonGeneric_Failure_Uses_Fallback()
    {
        var r = AxisResult.Error(E1).OrElse(_ => AxisResult.Ok());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void OrElse_NonGeneric_Failure_Fallback_Also_Fails()
    {
        var r = AxisResult.Error(E1).OrElse(_ => AxisResult.Error(E2));
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public void OrElse_NonGeneric_Combine_Errors_Concats()
    {
        var r = AxisResult.Error(E1).OrElse(_ => AxisResult.Error(E2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void OrElse_NonGeneric_Combine_Errors_Success_Fallback_Returns_Ok()
    {
        var r = AxisResult.Error(E1).OrElse(_ => AxisResult.Ok(), combineErrors: true);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void OrElse_NonGeneric_Combine_Errors_Success_Skip()
    {
        var src = AxisResult.Ok();
        var called = false;
        var r = src.OrElse(_ => { called = true; return AxisResult.Error(E2); }, combineErrors: true);
        Assert.False(called);
        Assert.Same(src, r);
    }

    [Fact]
    public void OrElse_NonGeneric_Combine_False_Replaces_Errors()
    {
        var r = AxisResult.Error(E1).OrElse(_ => AxisResult.Error(E2), combineErrors: false);
        Assert.Single(r.Errors);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region OrElse (generic)

    [Fact]
    public void OrElse_Generic_Success_Skips()
    {
        var src = AxisResult.Ok(5);
        var r = src.OrElse(_ => AxisResult.Ok(10));
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public void OrElse_Generic_Failure_Uses_Fallback()
    {
        var r = AxisResult.Error<int>(E1).OrElse(_ => AxisResult.Ok(99));
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public void OrElse_Generic_Combine_Errors()
    {
        var r = AxisResult.Error<int>(E1).OrElse(_ => AxisResult.Error<int>(E2), combineErrors: true);
        Assert.Equal(2, r.Errors.Count);
    }

    [Fact]
    public void OrElse_Generic_Combine_Errors_Success_Fallback()
    {
        var r = AxisResult.Error<int>(E1).OrElse(_ => AxisResult.Ok(42), combineErrors: true);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void OrElse_Generic_Combine_Errors_Success_Skip()
    {
        var src = AxisResult.Ok(5);
        var r = src.OrElse(_ => AxisResult.Ok(9), combineErrors: true);
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public void OrElse_Generic_Combine_False_Replaces()
    {
        var r = AxisResult.Error<int>(E1).OrElse(_ => AxisResult.Error<int>(E2), combineErrors: false);
        Assert.Single(r.Errors);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region Ensure

    [Fact]
    public void Ensure_Predicate_Success_Passes()
    {
        var r = AxisResult.Ok(10).Ensure(x => x > 0, E1);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Ensure_Predicate_Success_Fails()
    {
        var r = AxisResult.Ok(-1).Ensure(x => x > 0, E1);
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void Ensure_Predicate_Already_Failure_Propagates()
    {
        var r = AxisResult.Error<int>(E2).Ensure(x => x > 0, E1);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public void Ensure_Validation_Success_Passes()
    {
        var r = AxisResult.Ok(10).Ensure(_ => AxisResult.Ok());
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Ensure_Validation_Fails_Returns_Errors()
    {
        var r = AxisResult.Ok(10).Ensure(_ => AxisResult.Error(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void Ensure_Validation_Already_Failure_Skips()
    {
        var called = false;
        var r = AxisResult.Error<int>(E2).Ensure(_ => { called = true; return AxisResult.Ok(); });
        Assert.False(called);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region ThenUnless

    [Fact]
    public void ThenUnless_PredicateTrue_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = AxisResult.Ok(10).ThenUnless(x => x > 0, _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public void ThenUnless_PredicateFalse_Next_Succeeds_Preserves_Value()
    {
        var nextCalled = false;
        var r = AxisResult.Ok(10).ThenUnless(x => x < 0, _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.True(nextCalled);
    }

    [Fact]
    public void ThenUnless_PredicateFalse_Next_Fails_Propagates_Error()
    {
        var r = AxisResult.Ok(10).ThenUnless(x => x < 0, _ => AxisResult.Error(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void ThenUnless_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = AxisResult.Error<int>(E2).ThenUnless(
            _ => { predicateCalled = true; return true; },
            _ => { nextCalled = true; return AxisResult.Ok(); });
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    #endregion

    #region ThenWhen

    [Fact]
    public void ThenWhen_PredicateFalse_Skips_Next_And_Preserves_Value()
    {
        var nextCalled = false;
        var r = AxisResult.Ok(10).ThenWhen(x => x < 0, x => { nextCalled = true; return AxisResult.Ok(x * 2); });
        Assert.True(r.IsSuccess);
        Assert.Equal(10, r.Value);
        Assert.False(nextCalled);
    }

    [Fact]
    public void ThenWhen_PredicateTrue_Next_Succeeds_Replaces_Value()
    {
        var r = AxisResult.Ok(10).ThenWhen(x => x > 0, x => AxisResult.Ok(x * 2));
        Assert.True(r.IsSuccess);
        Assert.Equal(20, r.Value);
    }

    [Fact]
    public void ThenWhen_PredicateTrue_Next_Fails_Propagates_Error()
    {
        var r = AxisResult.Ok(10).ThenWhen(x => x > 0, _ => AxisResult.Error<int>(E1));
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void ThenWhen_Already_Failure_Skips_Predicate_And_Next()
    {
        var predicateCalled = false;
        var nextCalled = false;
        var r = AxisResult.Error<int>(E2).ThenWhen(
            _ => { predicateCalled = true; return true; },
            x => { nextCalled = true; return AxisResult.Ok(x); });
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
        Assert.False(predicateCalled);
        Assert.False(nextCalled);
    }

    #endregion

    #region Zip

    [Fact]
    public void Zip_PureMapper_Success()
    {
        var r = AxisResult.Ok(5).Zip(x => x * 2);
        Assert.Equal((5, 10), r.Value);
    }

    [Fact]
    public void Zip_PureMapper_Failure_Propagates()
    {
        var r = AxisResult.Error<int>(E1).Zip(x => x * 2);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Zip_ResultMapper_Success()
    {
        var r = AxisResult.Ok(5).Zip(x => AxisResult.Ok(x + 1));
        Assert.Equal((5, 6), r.Value);
    }

    [Fact]
    public void Zip_ResultMapper_MapperFails()
    {
        var r = AxisResult.Ok(5).Zip(_ => AxisResult.Error<int>(E1));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void Zip_ResultMapper_SourceFails_Skips_Mapper()
    {
        var called = false;
        var r = AxisResult.Error<int>(E1).Zip(x => { called = true; return AxisResult.Ok(x); });
        Assert.False(called);
        Assert.True(r.IsFailure);
    }

    #endregion

    #region Recover

    [Fact]
    public void Recover_Success_Returns_Same()
    {
        var src = AxisResult.Ok(5);
        var r = src.Recover(_ => 99);
        Assert.Same(src, r);
    }

    [Fact]
    public void Recover_Failure_Uses_Recovery()
    {
        var r = AxisResult.Error<int>(E1).Recover(errs => errs.Count * 10);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public void Recover_NoArgs_Failure_Uses_Recovery()
    {
        var r = AxisResult.Error<int>(E1).Recover(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void Recover_NoArgs_Success_Returns_Same()
    {
        var src = AxisResult.Ok(5);
        var r = src.Recover(() => 99);
        Assert.Same(src, r);
    }

    [Fact]
    public void Recover_Default_Failure()
    {
        var r = AxisResult.Error<int>(E1).Recover(7);
        Assert.Equal(7, r.Value);
    }

    [Fact]
    public void Recover_Default_Success()
    {
        var src = AxisResult.Ok(5);
        var r = src.Recover(7);
        Assert.Same(src, r);
    }

    #endregion

    #region RecoverWhen

    [Fact]
    public void RecoverWhen_Predicate_Match_Recovers()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen(errs => errs.Count == 1, _ => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public void RecoverWhen_Predicate_NoMatch_KeepsFailure()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen(errs => errs.Count > 5, _ => 99);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void RecoverWhen_Predicate_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = src.RecoverWhen(_ => true, _ => 9);
        Assert.Same(src, r);
    }

    [Fact]
    public void RecoverWhen_Type_Match_Recovers()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen(AxisErrorType.NotFound, () => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public void RecoverWhen_Type_NoMatch_KeepsFailure()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen(AxisErrorType.Conflict, () => 99);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void RecoverWhen_Type_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = src.RecoverWhen(AxisErrorType.NotFound, () => 9);
        Assert.Same(src, r);
    }

    [Fact]
    public void RecoverWhen_Code_Match_Recovers()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen("E1", () => 99);
        Assert.Equal(99, r.Value);
    }

    [Fact]
    public void RecoverWhen_Code_NoMatch_KeepsFailure()
    {
        var r = AxisResult.Error<int>(E1).RecoverWhen("X", () => 99);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void RecoverWhen_Code_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = src.RecoverWhen("E1", () => 9);
        Assert.Same(src, r);
    }

    [Fact]
    public void RecoverNotFound_AllNotFound_Recovers()
    {
        var r = AxisResult.Error<int>(new[] { E1, AxisError.NotFound("X") }).RecoverNotFound(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void RecoverNotFound_MixedErrors_KeepsFailure()
    {
        var r = AxisResult.Error<int>(new[] { E1, E2 }).RecoverNotFound(() => 42);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void RecoverNotFound_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = src.RecoverNotFound(() => 99);
        Assert.Same(src, r);
    }

    #endregion

    #region RecoverConflict

    [Fact]
    public void RecoverConflict_AllConflict_Recovers()
    {
        var r = AxisResult.Error<int>(new[] { AxisError.Conflict("C1"), AxisError.Conflict("C2") }).RecoverConflict(() => 42);
        Assert.Equal(42, r.Value);
    }

    [Fact]
    public void RecoverConflict_MixedErrors_KeepsFailure()
    {
        var r = AxisResult.Error<int>(new[] { AxisError.Conflict("C1"), E2 }).RecoverConflict(() => 42);
        Assert.True(r.IsFailure);
    }

    [Fact]
    public void RecoverConflict_Success_Skips()
    {
        var src = AxisResult.Ok(1);
        var r = src.RecoverConflict(() => 99);
        Assert.Same(src, r);
    }

    [Fact]
    public void RecoverConflict_ResultRecovery_PropagatesRecoveryFailure()
    {
        var r = AxisResult.Error<int>(AxisError.Conflict("C1")).RecoverConflict(() => AxisResult.Error<int>(E2));
        Assert.True(r.IsFailure);
        Assert.Equal("E2", Assert.Single(r.Errors).Code);
    }

    #endregion

    #region ElseNotFound<TNew>

    [Fact]
    public void ElseNotFound_Success_MapsValue()
    {
        var r = AxisResult.Ok(21).ElseNotFound(x => $"value:{x}", () => "default");
        Assert.Equal("value:21", r.Value);
    }

    [Fact]
    public void ElseNotFound_AllNotFound_UsesRecovery()
    {
        var r = AxisResult.Error<int>(new[] { E1, AxisError.NotFound("X") }).ElseNotFound(x => $"value:{x}", () => "default");
        Assert.Equal("default", r.Value);
    }

    [Fact]
    public void ElseNotFound_OtherError_PropagatesErrors()
    {
        var r = AxisResult.Error<int>(E2).ElseNotFound(x => $"value:{x}", () => "default");
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    [Fact]
    public void ElseNotFound_MixedErrors_PropagatesErrors()
    {
        var r = AxisResult.Error<int>(new[] { E1, E2 }).ElseNotFound(x => $"value:{x}", () => "default");
        Assert.True(r.IsFailure);
        Assert.Equal(2, r.Errors.Count);
    }

    #endregion

    #region LINQ

    [Fact]
    public void Select_Is_Map()
    {
        var r = AxisResult.Ok(5).Select(x => x * 2);
        Assert.Equal(10, r.Value);
    }

    [Fact]
    public void SelectMany_Success_Chains()
    {
        var r =
            from a in AxisResult.Ok(2)
            from b in AxisResult.Ok(3)
            select a + b;
        Assert.Equal(5, r.Value);
    }

    [Fact]
    public void SelectMany_First_Fails_Propagates()
    {
        var r =
            from a in AxisResult.Error<int>(E1)
            from b in AxisResult.Ok(3)
            select a + b;
        Assert.True(r.IsFailure);
        Assert.Equal("E1", r.Errors[0].Code);
    }

    [Fact]
    public void SelectMany_Second_Fails_Propagates()
    {
        var r =
            from a in AxisResult.Ok(2)
            from b in AxisResult.Error<int>(E2)
            select a + b;
        Assert.True(r.IsFailure);
        Assert.Equal("E2", r.Errors[0].Code);
    }

    #endregion

    #region RequireNotFound (non-generic)

    [Fact]
    public void RequireNotFound_NG_Success_ReturnsError()
    {
        var r = AxisResult.Ok().RequireNotFound(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "ALREADY_EXISTS");
    }

    [Fact]
    public void RequireNotFound_NG_AllNotFound_ReturnsOk()
    {
        var r = AxisResult.Error(AxisError.NotFound("NF")).RequireNotFound(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void RequireNotFound_NG_MixedErrors_PropagatesOriginal()
    {
        var errors = new[] { AxisError.NotFound("NF"), AxisError.ValidationRule("VR") };
        var r = AxisResult.Error(errors).RequireNotFound(AxisError.BusinessRule("ALREADY_EXISTS"));
        Assert.True(r.IsFailure);
        Assert.Contains(r.Errors, e => e.Code == "VR");
    }

    #endregion
}
