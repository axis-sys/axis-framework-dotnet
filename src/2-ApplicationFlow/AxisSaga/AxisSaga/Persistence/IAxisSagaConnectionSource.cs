using System.Data.Common;

namespace Axis.Persistence;

/// <summary>
/// The single seam the dialect-agnostic <see cref="Saga.AxisSagaSettingsStore"/> needs: open a raw ADO.NET
/// connection to the AXIS_SAGA store. Both dialect data-source wrappers implement it over their
/// <see cref="DbDataSource"/> (<c>NpgsqlDataSource</c> and <c>MySqlDataSource</c> both derive from it), so
/// the portable settings SQL lives in ONE place instead of once per dialect. It is <c>internal</c> — an
/// implementation detail of the settings store, exposed to the dialect adapters through
/// <c>InternalsVisibleTo</c>, not part of the public API surface.
/// </summary>
internal interface IAxisSagaConnectionSource
{
    ValueTask<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken);
}
