namespace Scaffolds.ECommerce.Contracts.Driven;

public interface IUnitOfWork
{
    Task<AxisResult> SaveChangesAsync();
}
