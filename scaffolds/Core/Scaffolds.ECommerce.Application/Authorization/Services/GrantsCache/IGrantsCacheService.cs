namespace Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;

internal interface IGrantsCacheService
{
    Task<AxisResult<Grants>> GetAsync(CustomerId customerId);
    Task<AxisResult> EvictAsync(CustomerId customerId);
}
