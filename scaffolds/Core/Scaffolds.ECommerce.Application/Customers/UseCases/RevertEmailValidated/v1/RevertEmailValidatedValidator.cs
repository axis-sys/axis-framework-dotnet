using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.RevertEmailValidated.v1;

internal sealed class RevertEmailValidatedValidator : AxisValidatorBase<RevertEmailValidatedCommand>
{
    public RevertEmailValidatedValidator()
    {
        RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
