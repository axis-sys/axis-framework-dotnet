using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class RequestTokenHandlerTests : AuthTestHost
{
    [Fact]
    public async Task IssuesBootstrapTokenWhenIdentityIsNewAsync()
    {
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.False(response.EmailValidated);
        Customers.Verify(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()), Times.Once);
        UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ReusesTheCustomerWhenIdentityIsKnownAsync()
    {
        var existing = new FakeCustomer(CustomerId.New, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: true);
        Customers.Setup(port => port.GetByEmailAsync(DefaultIdentity.Email!))
            .ReturnsAsync(AxisResult.Ok<ICustomerEntityProperties>(existing));
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.Equal((string)existing.CustomerId, response.CustomerId);
        Assert.True(response.EmailValidated);
        Customers.Verify(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task RecoversTheWinnerWhenTwoFirstLoginsRaceAsync()
    {
        var winner = new FakeCustomer(CustomerId.New, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: false);
        Customers.SetupSequence(port => port.GetByEmailAsync(DefaultIdentity.Email!))
            .ReturnsAsync(AxisError.NotFound("CUSTOMER_NOT_FOUND"))
            .ReturnsAsync(AxisResult.Ok<ICustomerEntityProperties>(winner));
        Customers.Setup(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()))
            .ReturnsAsync(AxisError.Conflict("CUSTOMER_EMAIL_ALREADY_EXISTS"));
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.Equal((string)winner.CustomerId, response.CustomerId);
        Customers.Verify(port => port.GetByEmailAsync(DefaultIdentity.Email!), Times.Exactly(2));
    }

    [Fact]
    public async Task FailsUnauthorizedWhenNoUsableIdentityAsync()
    {
        ExternalIdentity.Setup(context => context.Get())
            .Returns(AxisError.Unauthorized("EXTERNAL_TOKEN_INVALID"));
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        result.ShouldFailWithCode("EXTERNAL_TOKEN_INVALID");
    }
}
