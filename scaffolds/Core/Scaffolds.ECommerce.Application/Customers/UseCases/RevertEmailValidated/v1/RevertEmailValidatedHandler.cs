using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.RevertEmailValidated.v1;

internal sealed class RevertEmailValidatedHandler(
    ICustomersWritePort customersWrite,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RevertEmailValidatedCommand, RevertEmailValidatedResponse>
{
    public Task<AxisResult<RevertEmailValidatedResponse>> HandleAsync(RevertEmailValidatedCommand command)
    {
        var customerId = (CustomerId)command.CustomerId!;

        return customersWrite.SetEmailValidatedAsync(customerId, emailValidated: false)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(new RevertEmailValidatedResponse
            {
                CustomerId = customerId,
                EmailValidated = false,
            });
    }
}
