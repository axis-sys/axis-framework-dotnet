using Scaffolds.ECommerce.Contracts.Driven.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.GetCustomer.v1;

internal sealed class GetCustomerHandler(ICustomersPort customers) : IAxisQueryHandler<GetCustomerQuery, GetCustomerResponse>
{
    public Task<AxisResult<GetCustomerResponse>> HandleAsync(GetCustomerQuery query)
        => customers.GetByIdAsync(query.CustomerId!)
            .MapAsync(customer => new GetCustomerResponse
            {
                CustomerId = customer.CustomerId,
                Email = customer.Email,
                Name = customer.Name,
                EmailValidated = customer.EmailValidated,
                IsAdmin = customer.IsAdmin,
            });
}
