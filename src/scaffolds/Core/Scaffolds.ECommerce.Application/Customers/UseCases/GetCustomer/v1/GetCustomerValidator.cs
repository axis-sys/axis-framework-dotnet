using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.GetCustomer.v1;

internal sealed class GetCustomerValidator : AxisValidatorBase<GetCustomerQuery>
{
    public GetCustomerValidator()
    {
        RequiredTryParse(x => x.CustomerId, CustomersErrors.CustomerIdInvalid, CustomerId.TryParse);
    }
}
