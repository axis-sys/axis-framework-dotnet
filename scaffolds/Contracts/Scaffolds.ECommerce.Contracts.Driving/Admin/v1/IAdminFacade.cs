using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

namespace Scaffolds.ECommerce.Contracts.Driving.Admin.v1;

public interface IAdminFacade
{
    Task<AxisResult<MigrateDatabaseResponse>> MigrateDatabaseAsync(MigrateDatabaseCommand command);
}
