using Scaffolds.ECommerce.Contracts.Driven.Admin;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

namespace Scaffolds.ECommerce.Application.Admin.UseCases.MigrateDatabase.v1;

internal sealed class MigrateDatabaseHandler(
    IDatabaseMigratorPort databaseMigrator
) : IAxisCommandHandler<MigrateDatabaseCommand, MigrateDatabaseResponse>
{
    public Task<AxisResult<MigrateDatabaseResponse>> HandleAsync(MigrateDatabaseCommand command)
        => databaseMigrator.MigrateAsync()
            .MapAsync(schemas => new MigrateDatabaseResponse { Schemas = schemas });
}
