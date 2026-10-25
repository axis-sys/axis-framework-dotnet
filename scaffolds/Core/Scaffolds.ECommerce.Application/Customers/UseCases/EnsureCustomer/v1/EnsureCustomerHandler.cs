using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Application.Customers.Aggregates;
using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;
using Scaffolds.ECommerce.Domain.Customers.Customers;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.EnsureCustomer.v1;

internal sealed class EnsureCustomerHandler(
    ICustomersReaderPort customersReader,
    ICustomerFactory customerFactory,
    IUnitOfWork unitOfWork,
    IOptions<CustomersOptions> options
) : IAxisCommandHandler<EnsureCustomerCommand, EnsureCustomerResponse>
{
    public Task<AxisResult<EnsureCustomerResponse>> HandleAsync(EnsureCustomerCommand command)
        => customersReader
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
                Provider = customer.Provider,
            });

    private Task<AxisResult<ICustomerEntityProperties>> CreateAsync(EnsureCustomerCommand command)
        => customerFactory
            .CreateAsync(new ICustomerFactory.NewArgs
            {
                Email = command.Email!,
                Name = command.Name!,
                IsAdmin = options.Value.BootstrapAdminExternalIds.Contains(command.ExternalId!),
                ExternalId = command.ExternalId!,
                Provider = command.Provider,
            })
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .RecoverConflictAsync(() => ReadTheWinnerAsync(command.Email!));

    private Task<AxisResult<ICustomerEntityProperties>> ReadTheWinnerAsync(string email)
        => unitOfWork
            .RenewAsync()
            .ThenAsync(() => customersReader.GetByEmailAsync(email));
}
