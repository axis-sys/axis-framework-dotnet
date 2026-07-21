namespace AxisBus.Repository.Ports;

/// <summary>
/// Creates the outbox schema for one dialect — the per-provider "apply" seam, run once on startup by the
/// hosted initializer. Idempotent: already-applied versions are skipped.
/// </summary>
public interface IAxisBusStorageInitializer
{
    Task InitializeAsync();
}
