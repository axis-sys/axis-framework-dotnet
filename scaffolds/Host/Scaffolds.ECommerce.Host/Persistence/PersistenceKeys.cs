namespace Scaffolds.ECommerce.Host.Persistence;

internal static class PersistenceKeys
{
    public const string DatabaseProvider = "Database:Provider";
    public const string DatabaseMaxConnections = "Database:MaxConnections";

    public const string SagaProvider = "Saga:Provider";
    public const string SagaMaxPoolSize = "Saga:MaxPoolSize";
    public const string SagaResumerEnabled = "Saga:ResumerEnabled";
    public const string SagaJanitorEnabled = "Saga:JanitorEnabled";

    public const string BusDispatcherEnabled = "Bus:DispatcherEnabled";
    public const string BusPollInterval = "Bus:PollInterval";

    public const string CacheL1Ttl = "Cache:L1Ttl";
    public const string CacheSweepEnabled = "Cache:SweepEnabled";
    public const string CacheSweepInterval = "Cache:SweepInterval";
}
