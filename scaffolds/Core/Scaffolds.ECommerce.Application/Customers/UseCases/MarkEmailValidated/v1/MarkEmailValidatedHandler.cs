using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

namespace Scaffolds.ECommerce.Application.Customers.UseCases.MarkEmailValidated.v1;

internal sealed class MarkEmailValidatedHandler(
    ICustomersReaderPort customersReader,
    ICustomersWritePort customersWrite,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<MarkEmailValidatedCommand, MarkEmailValidatedResponse>
{
    public Task<AxisResult<MarkEmailValidatedResponse>> HandleAsync(MarkEmailValidatedCommand command)
    {
        var customerId = (CustomerId)command.CustomerId!;

        return customersReader.GetByIdAsync(customerId)
            .ThenAsync(customer => customer.EmailValidated
                ? AlreadyValidated(customerId).AsTaskAsync()
                : MarkAsync(customerId));
    }

    private Task<AxisResult<MarkEmailValidatedResponse>> MarkAsync(CustomerId customerId)
        => customersWrite.SetEmailValidatedAsync(customerId, emailValidated: true)
            .ThenAsync(unitOfWork.SaveChangesAsync)
            .WithValueAsync(new MarkEmailValidatedResponse
            {
                CustomerId = customerId,
                EmailValidated = true,
                AlreadyValidated = false,
            });

    private static AxisResult<MarkEmailValidatedResponse> AlreadyValidated(CustomerId customerId)
        => new MarkEmailValidatedResponse
        {
            CustomerId = customerId,
            EmailValidated = true,
            AlreadyValidated = true,
        };
}
