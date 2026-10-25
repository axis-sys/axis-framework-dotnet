using MySqlConnector;
using Npgsql;

namespace Scaffolds.ECommerce.Host.Persistence;

internal static class ConnectionPools
{
    public static int MaxPoolSize(DatabaseProvider provider, string connectionString) => provider switch
    {
        DatabaseProvider.Postgres => new NpgsqlConnectionStringBuilder(connectionString).MaxPoolSize,
        DatabaseProvider.MySql => checked((int)new MySqlConnectionStringBuilder(connectionString).MaximumPoolSize),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static string WithMaxPoolSize(DatabaseProvider provider, string connectionString, int maxPoolSize) => provider switch
    {
        DatabaseProvider.Postgres => new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize }.ConnectionString,
        DatabaseProvider.MySql => new MySqlConnectionStringBuilder(connectionString) { MaximumPoolSize = checked((uint)maxPoolSize) }.ConnectionString,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static void EnsureFitOneServer(int maxConnections, params int[] poolSizes)
    {
        if (poolSizes.Sum() > maxConnections)
            throw new InvalidOperationException(
                $"The connection pools sharing one server ({string.Join(" + ", poolSizes)}) exceed {PersistenceKeys.DatabaseMaxConnections} ({maxConnections}).");
    }
}
