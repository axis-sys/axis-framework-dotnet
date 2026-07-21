using Axis.SharedKernel;
using Scaffolds.ECommerce.Application.EmailValidation.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Validate;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class ValidateEmailHandlerTests : AuthTestHost
{
    [Fact]
    public async Task AcceptsTheRunAndStartsTheSagaWhenTheCodeMatchesAsync()
    {
        ValidationCodes.Setup(port => port.GetAsync(It.IsAny<CustomerId>())).ReturnsAsync(AxisResult.Ok("abc123"));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.ValidateAsync(new ValidateEmailCommand { Code = "abc123" });

        var response = result.ShouldSucceed();
        Assert.False(string.IsNullOrWhiteSpace(response.SagaId));
        Assert.Equal(AxisSagaStatus.Pending.ToString(), response.Status);
        SagaMediator.Verify(saga => saga.StartAsync(ValidateEmailSaga.Name, It.IsAny<ValidateEmailPayload>()), Times.Once);
    }

    [Fact]
    public async Task FailsFastWhenTheCodeMismatchesWithoutStartingTheSagaAsync()
    {
        ValidationCodes.Setup(port => port.GetAsync(It.IsAny<CustomerId>())).ReturnsAsync(AxisResult.Ok("abc123"));
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.ValidateAsync(new ValidateEmailCommand { Code = "wrong" });

        result.ShouldFailWithCode("EMAIL_VALIDATION_CODE_MISMATCH");
        SagaMediator.Verify(saga => saga.StartAsync(It.IsAny<string>(), It.IsAny<object>()), Times.Never);
    }
}
