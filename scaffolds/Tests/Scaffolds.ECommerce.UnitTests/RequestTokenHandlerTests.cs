using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.SharedKernel.Auth;

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
        CustomersWrite.Verify(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()), Times.Once);
        UnitOfWork.Verify(unit => unit.SaveChangesAsync(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SignsTheIdentityClaimsForTheConfiguredLifetimeWhenIssuingAsync()
    {
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.Equal("unit-test-access-token", response.AccessToken);
        AccessTokenSigner.Verify(signer => signer.Sign(It.Is<AccessTokenDescriptor>(descriptor =>
            descriptor.Claims[AuthClaimTypes.CustomerId] == response.CustomerId
            && descriptor.Claims[AuthClaimTypes.ExternalId] == DefaultIdentity.Subject
            && descriptor.ExpiresAt - descriptor.IssuedAt == TimeSpan.FromSeconds(300)
            && descriptor.ExpiresAt == response.AccessTokenExpiresAt)), Times.Once);
    }

    [Fact]
    public async Task ReusesTheCustomerWhenIdentityIsKnownAsync()
    {
        var existing = new FakeCustomer(CustomerId.New, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: true);
        CustomersReader.Setup(port => port.GetByEmailAsync(DefaultIdentity.Email!))
            .ReturnsAsync(AxisResult.Ok<ICustomerEntityProperties>(existing));
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.Equal((string)existing.CustomerId, response.CustomerId);
        Assert.True(response.EmailValidated);
        CustomersWrite.Verify(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task RecoversTheWinnerWhenTwoFirstLoginsRaceAsync()
    {
        var winner = new FakeCustomer(CustomerId.New, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: false, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: false);
        CustomersReader.SetupSequence(port => port.GetByEmailAsync(DefaultIdentity.Email!))
            .ReturnsAsync(CustomerErrors.CustomerNotFound)
            .ReturnsAsync(AxisResult.Ok<ICustomerEntityProperties>(winner));
        CustomersWrite.Setup(port => port.CreateAsync(It.IsAny<ICustomerEntityProperties>()))
            .ReturnsAsync(CustomerErrors.CustomerEmailAlreadyExists);
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        var response = result.ShouldSucceed();
        Assert.Equal((string)winner.CustomerId, response.CustomerId);
        CustomersReader.Verify(port => port.GetByEmailAsync(DefaultIdentity.Email!), Times.Exactly(2));
        UnitOfWork.Verify(unit => unit.RenewAsync(), Times.Once);
    }

    [Fact]
    public async Task FailsUnauthorizedWhenNoUsableIdentityAsync()
    {
        ExternalIdentity.Setup(context => context.Get())
            .Returns(AuthErrors.ExternalTokenInvalid);
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        result.ShouldFailWithCode(AuthErrors.ExternalTokenInvalid.Code);
    }

    [Fact]
    public async Task SignsOnlyTheCoarseAdministratorRoleWhenTheCustomerIsAnAdminAsync()
    {
        var admin = new FakeCustomer(CustomerId.New, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: true, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: true);
        CustomersReader.Setup(port => port.GetByEmailAsync(DefaultIdentity.Email!))
            .ReturnsAsync(AxisResult.Ok<ICustomerEntityProperties>(admin));
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        result.ShouldSucceed();
        AccessTokenSigner.Verify(signer => signer.Sign(It.Is<AccessTokenDescriptor>(descriptor =>
            descriptor.Claims[AuthClaimTypes.Role] == AuthRoles.Administrator)), Times.Once);
    }

    [Fact]
    public async Task SignsNoRoleWhenTheCustomerIsNotAnAdminAsync()
    {
        var facade = Build<IAuthFacade>();

        var result = await facade.RequestTokenAsync(new RequestTokenCommand());

        result.ShouldSucceed();
        AccessTokenSigner.Verify(signer => signer.Sign(It.Is<AccessTokenDescriptor>(descriptor =>
            !descriptor.Claims.ContainsKey(AuthClaimTypes.Role))), Times.Once);
    }
}
