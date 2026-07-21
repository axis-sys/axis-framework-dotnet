using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public interface IValidationCodesPort
{
    Task<AxisResult> SaveAsync(CustomerId customerId, string code);
    Task<AxisResult<string>> GetAsync(CustomerId customerId);
    Task<AxisResult> RemoveAsync(CustomerId customerId);
}
