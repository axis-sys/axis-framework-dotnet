using System.Security.Cryptography;
using Scaffolds.ECommerce.Contracts.Driven.Auth;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestCode;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.RequestCode.v1;

internal sealed class RequestEmailValidationHandler(
    IAxisMediator mediator,
    ICustomersFacade customersFacade,
    IValidationCodesPort validationCodes,
    IAxisEmailService email,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RequestEmailValidationCommand, RequestEmailValidationResponse>
{
    // GetCustomerAsync only reads, but ValidationCodes is a real repository write now — commit it. The
    // email outbox stays in-memory (a test double, not business state) and never touches the unit of work.
    public Task<AxisResult<RequestEmailValidationResponse>> HandleAsync(RequestEmailValidationCommand command)
    {
        var code = RandomNumberGenerator.GetHexString(8, lowercase: true);
        return customersFacade
            .GetCustomerAsync(new GetCustomerQuery { CustomerId = mediator.AxisEntityId })
            .ThenAsync(customer => validationCodes.SaveAsync(customer.CustomerId, code))
            .ThenAsync(_ => unitOfWork.SaveChangesAsync())
            .ThenAsync(customer => email.SendAsync(new AxisEmailData
            {
                To = [(customer.Name ?? customer.Email!, customer.Email!)],
                Subject = "Validate your email",
                Body = $"Your validation code is {code}.",
            }))
            .MapAsync(customer => new RequestEmailValidationResponse { SentTo = customer.Email! });
    }
}
