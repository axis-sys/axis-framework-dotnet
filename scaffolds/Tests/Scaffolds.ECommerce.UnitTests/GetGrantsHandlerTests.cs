using Scaffolds.ECommerce.Application.Authorization.Services.GrantsCache;
using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class GetGrantsHandlerTests : AuthTestHost
{
    private static readonly CustomerId Customer = CustomerId.New;

    [Fact]
    public async Task ReportsTheMembershipPermissionsWhenTheCustomerIsAMemberAsync()
    {
        MembersReader.Setup(port => port.GetByCustomerIdAsync(Customer))
            .ReturnsAsync(AxisResult.Ok<IMemberEntityProperties>(new FakeMember(Customer, Disabled: false, [PermissionCodes.CatalogRegisterProducts])));
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        var grants = result.ShouldSucceed();
        Assert.True(grants.IsMember);
        Assert.False(grants.IsDisabled);
        Assert.False(grants.IsAdministrator);
        Assert.Equal([PermissionCodes.CatalogRegisterProducts], grants.Permissions);
    }

    [Fact]
    public async Task ReportsNoMembershipWhenTheCustomerIsNotAMemberAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        var grants = result.ShouldSucceed();
        Assert.False(grants.IsMember);
        Assert.Empty(grants.Permissions);
    }

    [Fact]
    public async Task ReportsTheDisabledMembershipWhenTheMemberIsDisabledAsync()
    {
        MembersReader.Setup(port => port.GetByCustomerIdAsync(Customer))
            .ReturnsAsync(AxisResult.Ok<IMemberEntityProperties>(new FakeMember(Customer, Disabled: true, [PermissionCodes.CatalogRegisterProducts])));
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        var grants = result.ShouldSucceed();
        Assert.True(grants.IsMember);
        Assert.True(grants.IsDisabled);
    }

    [Fact]
    public async Task ReportsTheAdministratorWhenTheCustomerIsAnAdminAsync()
    {
        CustomersReader.Setup(port => port.GetByIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync((CustomerId id) => AxisResult.Ok<ICustomerEntityProperties>(
                new FakeCustomer(id, DefaultIdentity.Email!, DefaultIdentity.Name!, IsAdmin: true, DefaultIdentity.Subject, DefaultIdentity.Provider, EmailValidated: true)));
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        var grants = result.ShouldSucceed();
        Assert.True(grants.IsAdministrator);
        Assert.False(grants.IsMember);
    }

    [Fact]
    public async Task FailsWithNotFoundWhenTheCustomerIsUnknownAsync()
    {
        CustomersReader.Setup(port => port.GetByIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(CustomerErrors.CustomerNotFound);
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        result.ShouldFailWithCode(CustomerErrors.CustomerNotFound.Code);
    }

    [Fact]
    public async Task ComposesTheGrantsUnderTheCustomerKeyWhenTheCacheMissesAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        result.ShouldSucceed();
        Cache.Verify(cache => cache.GetOrCreateAsync(
            It.Is<string>(key => key.EndsWith((string)Customer, StringComparison.Ordinal)),
            It.IsAny<Func<Task<AxisResult<Grants>>>>(),
            It.Is<TimeSpan?>(ttl => ttl > TimeSpan.Zero)), Times.Once);
        MembersReader.Verify(port => port.GetByCustomerIdAsync(Customer), Times.Once);
    }

    [Fact]
    public async Task ServesTheCachedGrantsWithoutReadingTheMembershipWhenTheCacheHoldsThemAsync()
    {
        Cache.Setup(cache => cache.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<AxisResult<Grants>>>>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(AxisResult.Ok(new Grants(Customer, IsAdministrator: false, IsMember: true, IsDisabled: false, [PermissionCodes.ValidateEmailRuns])));
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(Query());

        Assert.Equal([PermissionCodes.ValidateEmailRuns], result.ShouldSucceed().Permissions);
        MembersReader.Verify(port => port.GetByCustomerIdAsync(It.IsAny<CustomerId>()), Times.Never);
        CustomersReader.Verify(port => port.GetByIdAsync(It.IsAny<CustomerId>()), Times.Never);
    }

    [Fact]
    public async Task FailsValidationWithoutReadingWhenTheCustomerIdIsNotAnIdAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.GetGrantsAsync(new GetGrantsQuery { CustomerId = "not-a-customer-id" });

        result.ShouldFailWithCode(SharedErrors.CustomerIdInvalid);
        Cache.Verify(cache => cache.GetOrCreateAsync(It.IsAny<string>(), It.IsAny<Func<Task<AxisResult<Grants>>>>(), It.IsAny<TimeSpan?>()), Times.Never);
    }

    private static GetGrantsQuery Query() => new() { CustomerId = Customer };
}
