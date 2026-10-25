using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.GetCustomer.v1;

internal sealed class GetCustomerHandler(ICustomersReaderPort customersReader) : IAxisQueryHandler<GetCustomerQuery, GetCustomerResponse>
{
    public Task<AxisResult<GetCustomerResponse>> HandleAsync(GetCustomerQuery query)
    {
        var customerId = (CustomerId)query.CustomerId!;

        return customersReader.GetByIdAsync(customerId)
            .MapAsync(customer => new GetCustomerResponse
            {
                CustomerId = customer.CustomerId,
                Email = customer.Email,
                Name = customer.Name,
                EmailValidated = customer.EmailValidated,
                IsAdmin = customer.IsAdmin,
                ExternalId = customer.ExternalId,
            });
    }
}
