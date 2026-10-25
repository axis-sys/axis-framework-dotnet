using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.MarkEmailValidated.v1;

internal sealed class MarkEmailValidatedValidator : AxisValidatorBase<MarkEmailValidatedCommand>
{
    public MarkEmailValidatedValidator()
    {
        RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
