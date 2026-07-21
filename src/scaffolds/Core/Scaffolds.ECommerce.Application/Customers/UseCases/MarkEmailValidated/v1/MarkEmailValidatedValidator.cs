using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.MarkEmailValidated.v1;

internal sealed class MarkEmailValidatedValidator : AxisValidatorBase<MarkEmailValidatedCommand>
{
    public MarkEmailValidatedValidator()
    {
        RequiredTryParse(x => x.CustomerId, CustomersErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
