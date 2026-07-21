using Axis.Persistence;
using Axis.Saga;
using Axis.SharedKernel;

namespace AxisSaga.UnitTests;

/// <summary>
/// Dialect-independent behaviour of <see cref="AxisSagaSettingsStore"/>: the input guards reject an
/// invalid cap BEFORE any database round-trip. The SQL paths themselves are covered by the Postgres and
/// MySQL integration suites (they need a real single-row SAGA_SETTINGS table).
/// </summary>
public class AxisSagaSettingsStoreTests
{
    private static AxisSagaSettingsStore Build(out Mock<IAxisSagaConnectionSource> source)
    {
        // Strict so an unexpected OpenConnectionAsync (i.e. a validation guard that leaked to the DB) fails.
        source = new Mock<IAxisSagaConnectionSource>(MockBehavior.Strict);
        return new AxisSagaSettingsStore(source.Object, Mock.Of<IAxisLogger<AxisSagaSettingsStore>>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task SetRejectsNonPositiveCapWithoutTouchingTheDatabaseAsync(int invalidCap)
    {
        var store = Build(out var source);

        var result = await store.SetMaxConcurrentSagasAsync(invalidCap, TestContext.Current.CancellationToken);

        result.ShouldFailWithCode(AxisSagaErrors.InvalidConcurrencyCap);
        source.Verify(s => s.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task TrySetRejectsNonPositiveNewValueWithoutTouchingTheDatabaseAsync(int invalidNewValue)
    {
        var store = Build(out var source);

        var result = await store.TrySetMaxConcurrentSagasAsync(20, invalidNewValue, TestContext.Current.CancellationToken);

        result.ShouldFailWithCode(AxisSagaErrors.InvalidConcurrencyCap);
        source.Verify(s => s.OpenConnectionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidCapIsAValidationRuleErrorAsync()
    {
        var store = Build(out _);

        var result = await store.SetMaxConcurrentSagasAsync(0, TestContext.Current.CancellationToken);

        result.ShouldFailWithType(AxisErrorType.ValidationRule);
    }
}
