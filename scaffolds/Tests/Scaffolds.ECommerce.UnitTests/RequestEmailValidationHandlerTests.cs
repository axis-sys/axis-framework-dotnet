using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class RequestEmailValidationHandlerTests : AuthTestHost
{
    [Fact]
    public async Task EmailsANewCodeToTheAddressOnRecordWhenTheCallerIsKnownAsync()
    {
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.RequestEmailValidationAsync(new RequestEmailValidationCommand());

        var response = result.ShouldSucceed();
        Assert.Equal(DefaultIdentity.Email, response.SentTo);
        ValidationCodesWrite.Verify(port => port.CreateAsync(It.Is<IValidationCodeEntityProperties>(
            code => code.CustomerId == (CustomerId)Identity!.Value.ToString() && code.Code.Length == 8)), Times.Once);
        Email.Verify(service => service.SendAsync(It.IsAny<AxisEmailData>()), Times.Once);
        UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task ReplacesThePendingCodeWhenTheCustomerAlreadyHasOneAsync()
    {
        ValidationCodesWrite.Setup(port => port.CreateAsync(It.IsAny<IValidationCodeEntityProperties>()))
            .ReturnsAsync(ValidationCodeErrors.ValidationCodeAlreadyExists);
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.RequestEmailValidationAsync(new RequestEmailValidationCommand());

        result.ShouldSucceed();
        ValidationCodesWrite.Verify(port => port.UpdateAsync(It.Is<IValidationCodeEntityProperties>(code => code.Code.Length == 8)), Times.Once);
        Email.Verify(service => service.SendAsync(It.IsAny<AxisEmailData>()), Times.Once);
    }

    [Fact]
    public async Task FailsUnauthorizedWithoutSendingWhenTheCallerIsAnonymousAsync()
    {
        Identity = null;
        var facade = Build<IEmailValidationFacade>();

        var result = await facade.RequestEmailValidationAsync(new RequestEmailValidationCommand());

        result.ShouldFailWithCode(SharedErrors.CustomerIdentityRequired.Code);
        Email.Verify(service => service.SendAsync(It.IsAny<AxisEmailData>()), Times.Never);
    }
}
