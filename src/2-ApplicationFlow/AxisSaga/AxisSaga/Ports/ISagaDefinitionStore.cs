namespace Axis.Ports;

/// <summary>
/// Persistence boundary for the saga-definition snapshot (<c>SAGA_DEFINITIONS</c>). The initializer
/// upserts the in-process definition; the row is purely informational/auditing (a hash-guarded
/// snapshot of the registered topology).
/// </summary>
public interface ISagaDefinitionStore
{
    /// <summary>
    /// Inserts or updates the definition snapshot, updating only when the hash actually changed.
    /// Returns <c>true</c> when a row was written (insert or a real change).
    /// </summary>
    Task<bool> UpsertAsync(string sagaName, string definitionHash, string definitionJson, CancellationToken cancellationToken);
}
