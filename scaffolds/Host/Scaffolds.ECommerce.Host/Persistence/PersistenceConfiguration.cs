using MySqlProvider = Scaffolds.ECommerce.Adapters.Driven.MySql.DependencyInjection;
using PostgresProvider = Scaffolds.ECommerce.Adapters.Driven.Postgres.DependencyInjection;

namespace Scaffolds.ECommerce.Host.Persistence;

internal static class PersistenceConfiguration
{
    private const DatabaseProvider DefaultProvider = DatabaseProvider.Postgres;

    public static DatabaseProvider Provider(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return DefaultProvider;

        return Enum.GetValues<DatabaseProvider>()
            .Where(provider => string.Equals(provider.ToString(), value, StringComparison.OrdinalIgnoreCase))
            .Select(provider => (DatabaseProvider?)provider)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Unknown {key} '{value}'; expected one of {string.Join(", ", Enum.GetNames<DatabaseProvider>())}.");
    }

    public static string ConnectionString(IConfiguration configuration, DatabaseProvider provider)
    {
        var name = ConnectionStringName(provider);
        var connectionString = configuration.GetConnectionString(name);
        return string.IsNullOrWhiteSpace(connectionString)
            ? throw new InvalidOperationException($"Missing ConnectionStrings:{name}.")
            : connectionString;
    }

    public static int PositiveInt(IConfiguration configuration, string key)
        => configuration.GetValue<int?>(key) is { } value and > 0
            ? value
            : throw new InvalidOperationException($"Missing or non-positive {key}.");

    private static string ConnectionStringName(DatabaseProvider provider) => provider switch
    {
        DatabaseProvider.Postgres => PostgresProvider.ConnectionStringName,
        DatabaseProvider.MySql => MySqlProvider.ConnectionStringName,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };
}
