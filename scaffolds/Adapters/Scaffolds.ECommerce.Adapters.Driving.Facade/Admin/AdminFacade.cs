using Scaffolds.ECommerce.Contracts.Driving.Admin.v1;
using Scaffolds.ECommerce.Contracts.Driving.Admin.v1.MigrateDatabase;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Admin;

internal sealed class AdminFacade(IAxisMediator mediator) : IAdminFacade
{
    public Task<AxisResult<MigrateDatabaseResponse>> MigrateDatabaseAsync(MigrateDatabaseCommand command)
        => mediator.Cqrs.ExecuteAsync<MigrateDatabaseCommand, MigrateDatabaseResponse>(command);
}
