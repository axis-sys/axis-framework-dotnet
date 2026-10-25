using Scaffolds.ECommerce.Application.EmailValidation.Aggregates;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.GetCustomer;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;

namespace Scaffolds.ECommerce.Application.EmailValidation.UseCases.RequestEmailValidation.v1;

internal sealed class RequestEmailValidationHandler(
    IAxisMediator mediator,
    ICustomersFacade customersFacade,
    IValidationCodeFactory validationCodeFactory,
    IAxisEmailService email,
    IUnitOfWork unitOfWork
) : IAxisCommandHandler<RequestEmailValidationCommand, RequestEmailValidationResponse>
{
    public Task<AxisResult<RequestEmailValidationResponse>> HandleAsync(RequestEmailValidationCommand command)
    {
        var callerId = mediator.AxisEntityId is { } caller ? (CustomerId?)caller.ToString() : null;

        return callerId.Rop()
            .Ensure(customerId => customerId.HasValue, SharedErrors.CustomerIdentityRequired)
            .ThenAsync(customerId => customersFacade.GetCustomerAsync(new GetCustomerQuery { CustomerId = customerId!.Value }))
            .ZipAsync(_ => validationCodeFactory.CreateAsync(new IValidationCodeFactory.NewArgs { CustomerId = callerId!.Value }))
            .ThenAsync((_, _) => unitOfWork.SaveChangesAsync())
            .ThenAsync((customer, validationCode) => email.SendAsync(new AxisEmailData
            {
                To = [(customer.Name, customer.Email)],
                Subject = "Validate your email",
                Body = $"Your validation code is {validationCode.Code}.",
            }))
            .MapAsync((customer, _) => new RequestEmailValidationResponse { SentTo = customer.Email });
    }
}
