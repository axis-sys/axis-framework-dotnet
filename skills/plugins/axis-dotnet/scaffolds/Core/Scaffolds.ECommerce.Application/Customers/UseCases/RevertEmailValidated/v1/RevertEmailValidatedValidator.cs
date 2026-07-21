using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.RevertEmailValidated.v1;

internal sealed class RevertEmailValidatedValidator : AxisValidatorBase<RevertEmailValidatedCommand>
{
    public RevertEmailValidatedValidator()
    {
        RequiredTryParse(x => x.CustomerId, CustomersErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
