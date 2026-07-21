namespace AxisCache.Repository.Ports;

/// <summary>
/// Creates the L2 cache schema for one dialect — the per-provider "apply" seam, run once on startup by the
/// hosted initializer. Idempotent: already-applied versions are skipped.
/// </summary>
public interface IAxisCacheStorageInitializer
{
    Task InitializeAsync();
}
