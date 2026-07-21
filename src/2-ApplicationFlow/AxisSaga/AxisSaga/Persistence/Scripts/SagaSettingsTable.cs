using Axis.Ddl;

namespace Axis.Persistence.Scripts;

internal static class SagaSettingsTable
{
    public const string Table = $"{AxisSagaSchema.Schema}.SAGA_SETTINGS";

    public const string OnlyRow = "ONLY_ROW";
    public const string MaxConcurrentSagas = "MAX_CONCURRENT_SAGAS";

    // Process-wide cap held in a SINGLE shared row (one source of truth across instances). The ONLY_ROW
    // boolean PK + CHECK pins the table to exactly one row. MAX_CONCURRENT_SAGAS: global live-lease cap read
    // by the claim; NULL = unbounded; change at runtime with an UPDATE, no redeploy.
    public static AxisTable Define() => new AxisTable(Table)
        .Column(OnlyRow, AxisDbType.Bool, primaryKey: true, @default: AxisDefault.Bool(true), check: AxisCheck.IsTrue)
        .Column(MaxConcurrentSagas, AxisDbType.Int)
        .WithSeed([OnlyRow, MaxConcurrentSagas], [OnlyRow], [true, 20]);
}
