namespace Axis.Ports;

/// <summary>
/// Bootstraps the saga storage schema for the configured dialect (runs the idempotent migrations).
/// Implemented per database so the dialect-agnostic resumer worker can migrate on startup without
/// knowing whether it is talking to Postgres, MySQL, or anything else.
/// </summary>
public interface IAxisSagaStorageInitializer
{
    Task InitializeAsync();
}
