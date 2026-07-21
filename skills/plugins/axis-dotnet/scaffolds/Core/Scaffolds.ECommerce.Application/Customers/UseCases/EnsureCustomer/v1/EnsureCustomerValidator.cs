using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.EnsureCustomer.v1;

internal sealed class EnsureCustomerValidator : AxisValidatorBase<EnsureCustomerCommand>
{
    public EnsureCustomerValidator()
    {
        RequiredWithMaxLength(x => x.ExternalId, CustomersErrors.ExternalIdRequired);
        RequiredEmail(x => x.Email, CustomersErrors.EmailInvalid);
        RequiredWithMaxLength(x => x.Name, CustomersErrors.NameRequired);
        RequiredWithMaxLength(x => x.Provider, CustomersErrors.ProviderRequired);
    }
}
