using Scaffolds.ECommerce.Contracts.Driven.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.RevertEmailValidated.v1;

internal sealed class RevertEmailValidatedHandler(
    ICustomersPort customers,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RevertEmailValidatedCommand, RevertEmailValidatedResponse>
{
    public Task<AxisResult<RevertEmailValidatedResponse>> HandleAsync(RevertEmailValidatedCommand command)
        => customers.SetEmailValidatedAsync(command.CustomerId!, emailValidated: false)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(new RevertEmailValidatedResponse
            {
                CustomerId = command.CustomerId!,
                EmailValidated = false,
            });
}
