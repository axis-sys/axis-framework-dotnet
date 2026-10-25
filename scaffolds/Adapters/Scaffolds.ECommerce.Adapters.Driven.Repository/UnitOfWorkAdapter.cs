namespace Scaffolds.ECommerce.Adapters.Driven.Repository;

internal sealed class UnitOfWorkAdapter(
    [FromKeyedServices(ApplicationConfig.AppKey)] IAxisUnitOfWork inner) : IUnitOfWork
{
    public Task<AxisResult> SaveChangesAsync() => inner.SaveChangesAsync();

    public Task<AxisResult> RenewAsync() => inner.RenewAsync();
}
