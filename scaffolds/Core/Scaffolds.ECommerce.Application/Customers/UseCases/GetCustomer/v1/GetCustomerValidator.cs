using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.GetCustomer.v1;

internal sealed class GetCustomerValidator : AxisValidatorBase<GetCustomerQuery>
{
    public GetCustomerValidator()
    {
        RequiredTryParse(x => x.CustomerId, SharedErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
