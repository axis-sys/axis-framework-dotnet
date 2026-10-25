namespace Scaffolds.ECommerce.Contracts.Driven.Admin;

public interface IDatabaseMigratorPort
{
    Task<AxisResult<IReadOnlyList<string>>> MigrateAsync();
}
