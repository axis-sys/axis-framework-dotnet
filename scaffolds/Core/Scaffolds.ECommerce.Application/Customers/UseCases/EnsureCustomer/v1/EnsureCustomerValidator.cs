using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.EnsureCustomer.v1;

internal sealed class EnsureCustomerValidator : AxisValidatorBase<EnsureCustomerCommand>
{
    public EnsureCustomerValidator()
    {
        RequiredWithMaxLength(x => x.ExternalId, CustomerErrors.ExternalIdRequired);
        RequiredEmail(x => x.Email, CustomerErrors.EmailInvalid);
        RequiredWithMaxLength(x => x.Name, SharedErrors.NameRequired);
        RequiredWithMaxLength(x => x.Provider, CustomerErrors.ProviderRequired);
    }
}
