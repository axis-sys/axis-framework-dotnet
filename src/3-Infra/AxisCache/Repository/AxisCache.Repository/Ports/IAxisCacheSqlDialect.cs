namespace AxisCache.Repository.Ports;

/// <summary>
/// The one piece of L2 SQL that genuinely diverges between databases: the upsert. Every other statement
/// (select, delete, delete-expired) is standard SQL shared by the core store. Parameters, in order:
/// <c>@key</c>, <c>@value</c> (the JSON payload), <c>@expiresAt</c> (nullable), <c>@updatedAt</c>.
/// </summary>
public interface IAxisCacheSqlDialect
{
    string UpsertSql { get; }
}
