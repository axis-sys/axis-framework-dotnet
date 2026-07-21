using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Contracts.Driven.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.EnsureCustomer.v1;

internal sealed class EnsureCustomerHandler(
    ICustomersPort customers,
    IUnitOfWork unitOfWork,
    IOptions<CustomersOptions> options
) : IAxisCommandHandler<EnsureCustomerCommand, EnsureCustomerResponse>
{
    public Task<AxisResult<EnsureCustomerResponse>> HandleAsync(EnsureCustomerCommand command)
        => customers
            .GetByEmailAsync(command.Email!)
            .RecoverNotFoundAsync(() => CreateAsync(command))
            .MapAsync(customer => new EnsureCustomerResponse
            {
                CustomerId = customer.CustomerId,
                ExternalId = customer.ExternalId,
                Email = customer.Email,
                Name = customer.Name,
                EmailValidated = customer.EmailValidated,
                IsAdmin = customer.IsAdmin,
                Provider = customer.Provider
            });

    private Task<AxisResult<ICustomerEntityProperties>> CreateAsync(EnsureCustomerCommand command)
        => ((ICustomerEntityProperties)new CustomerProperties(
            CustomerId.New,
            command.Email!,
            command.Name!,
            IsAdmin: options.Value.BootstrapAdminExternalIds.Contains(command.ExternalId!),
            command.ExternalId!,
            command.Provider,
            EmailValidated: false
        )).Rop()
        .ThenAsync(customers.CreateAsync)
        .ThenAsync(_ => unitOfWork.SaveChangesAsync())
        .RecoverConflictAsync(() => customers.GetByEmailAsync(command.Email!));
}
