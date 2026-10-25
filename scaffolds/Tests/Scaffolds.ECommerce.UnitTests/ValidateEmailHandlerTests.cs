using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class ValidateEmailHandlerTests : AuthTestHost
{
    [Fact]
    public async Task AcceptsTheRunAndStartsTheSagaWhenTheCodeMatchesAsync()
    {
        ValidationCodesReader.Setup(port => port.GetByCustomerIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync((CustomerId customerId) => AxisResult.Ok<IValidationCodeEntityProperties>(new FakeValidationCode(customerId, "abc123")));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.ValidateEmailAsync(new ValidateEmailCommand { Code = "abc123" });

        var response = result.ShouldSucceed();
        Assert.False(string.IsNullOrWhiteSpace(response.SagaId));
        Assert.Equal(AxisSagaStatus.Pending.ToString(), response.Status);
        SagaMediator.Verify(saga => saga.StartAsync(
            ValidateEmailSaga.Name,
            It.Is<ValidateEmailPayload>(payload => payload.CustomerId == Identity!.Value.ToString())), Times.Once);
    }

    [Fact]
    public async Task FailsFastWhenTheCodeMismatchesWithoutStartingTheSagaAsync()
    {
        ValidationCodesReader.Setup(port => port.GetByCustomerIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync((CustomerId customerId) => AxisResult.Ok<IValidationCodeEntityProperties>(new FakeValidationCode(customerId, "abc123")));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.ValidateEmailAsync(new ValidateEmailCommand { Code = "wrong" });

        result.ShouldFailWithCode(ValidationCodeErrors.CodeMismatch.Code);
        SagaMediator.Verify(saga => saga.StartAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
