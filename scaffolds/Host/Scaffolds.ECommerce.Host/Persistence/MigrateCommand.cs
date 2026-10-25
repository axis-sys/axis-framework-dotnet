using Scaffolds.ECommerce.Contracts.Driving.Admin.v1;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

namespace Scaffolds.ECommerce.Host.Persistence;

internal sealed class MigrateCommand(IAdminFacade adminFacade, IAxisLogger<MigrateCommand> logger)
{
    public const string Verb = "migrate";
    public const int Succeeded = 0;
    public const int Failed = 1;

    public static bool IsRequested(string[] args) => args is [Verb, ..];

    public static async Task<int> RunAsync(WebApplication app)
    {
        await using (app)
        {
            await using var scope = app.Services.CreateAsyncScope();
            return await ActivatorUtilities.CreateInstance<MigrateCommand>(scope.ServiceProvider).MigrateAsync();
        }
    }

    private Task<int> MigrateAsync()
        => adminFacade
            .MigrateDatabaseAsync(new MigrateDatabaseCommand())
            .MatchAsync(Migrated, NotMigrated);

    private int Migrated(MigrateDatabaseResponse migrated)
    {
        logger.LogInformation("Database migrated", ("schemas", string.Join(", ", migrated.Schemas)));
        return Succeeded;
    }

    private int NotMigrated(IReadOnlyList<AxisError> errors)
    {
        logger.LogError("Database migration failed", ("errors", string.Join(", ", errors.Select(error => error.Code))));
        return Failed;
    }
}
