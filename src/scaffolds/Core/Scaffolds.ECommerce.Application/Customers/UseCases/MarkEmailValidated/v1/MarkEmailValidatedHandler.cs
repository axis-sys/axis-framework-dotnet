using Scaffolds.ECommerce.Contracts.Driven.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.MarkEmailValidated.v1;

internal sealed class MarkEmailValidatedHandler(
    ICustomersPort customers,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<MarkEmailValidatedCommand, MarkEmailValidatedResponse>
{
    public Task<AxisResult<MarkEmailValidatedResponse>> HandleAsync(MarkEmailValidatedCommand command)
        => customers.GetByIdAsync(command.CustomerId!)
            .ThenAsync(customer => customer.EmailValidated
                ? AlreadyValidated(command.CustomerId!).AsTaskAsync()
                : MarkAsync(command.CustomerId!));

    private Task<AxisResult<MarkEmailValidatedResponse>> MarkAsync(string customerId)
        => customers.SetEmailValidatedAsync(customerId, emailValidated: true)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(new MarkEmailValidatedResponse
            {
                CustomerId = customerId,
                EmailValidated = true,
                AlreadyValidated = false,
            });

    private static AxisResult<MarkEmailValidatedResponse> AlreadyValidated(string customerId)
        => new MarkEmailValidatedResponse
        {
            CustomerId = customerId,
            EmailValidated = true,
            AlreadyValidated = true,
        };
}
