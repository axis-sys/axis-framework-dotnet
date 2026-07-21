namespace Axis;

/// <summary>
/// Dialect-agnostic named-parameter binder handed to <see cref="IAxisDbRepository"/> callers. It wraps
/// the underlying ADO.NET command and creates a provider parameter per <see cref="Add"/> call, so a
/// shared repository never names a concrete provider type (NpgsqlParameter, MySqlParameter, …). Use
/// named placeholders in the SQL (<c>@id</c>).
/// </summary>
public interface IDbParamBinder
{
    /// <summary>Adds a named parameter. A leading <c>@</c> in <paramref name="name"/> is ignored; a null value becomes <c>DBNull</c>.</summary>
    IDbParamBinder Add(string name, object? value);

    /// <summary>
    /// Adds a JSON-valued parameter. Semantically the same as <see cref="Add"/> (the value is a JSON string);
    /// any dialect cast (Postgres <c>::JSONB</c>) lives in the SQL provided by the dialect, not here.
    /// </summary>
    IDbParamBinder AddJson(string name, string? json);
}
