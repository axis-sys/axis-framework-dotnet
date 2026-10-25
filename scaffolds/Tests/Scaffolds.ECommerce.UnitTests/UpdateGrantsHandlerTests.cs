using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Authorization;
using Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

namespace Scaffolds.ECommerce.UnitTests;

public sealed class UpdateGrantsHandlerTests : AuthTestHost
{
    private static readonly CustomerId Customer = CustomerId.New;

    [Fact]
    public async Task CreatesTheMembershipAndPublishesTheChangeBeforeCommittingWhenTheCustomerIsNotYetAMemberAsync()
    {
        List<string> calls = [];
        List<(GrantsUpdatedEvent Event, string[] Topics)> published = [];
        MembersWrite.Setup(port => port.CreateAsync(It.IsAny<IMemberEntityProperties>()))
            .Callback(() => calls.Add(nameof(IMembersWritePort.CreateAsync)))
            .ReturnsAsync((IMemberEntityProperties member) => AxisResult.Ok(member));
        Bus.Setup(bus => bus.PublishAsync(It.IsAny<GrantsUpdatedEvent>(), It.IsAny<string[]>()))
            .Callback((GrantsUpdatedEvent @event, string[] topics) =>
            {
                calls.Add(nameof(IAxisBus.PublishAsync));
                published.Add((@event, topics));
            })
            .ReturnsAsync(AxisResult.Ok());
        UnitOfWork.Setup(unit => unit.SaveChangesAsync())
            .Callback(() => calls.Add(nameof(IUnitOfWork.SaveChangesAsync)))
            .ReturnsAsync(AxisResult.Ok());
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(Command(disabled: false, PermissionCodes.CatalogRegisterProducts));

        var grants = result.ShouldSucceed();
        Assert.Equal((string)Customer, grants.CustomerId);
        Assert.Equal([PermissionCodes.CatalogRegisterProducts], grants.Permissions);
        Assert.Equal([nameof(IMembersWritePort.CreateAsync), nameof(IAxisBus.PublishAsync), nameof(IUnitOfWork.SaveChangesAsync)], calls);
        var (@event, topics) = Assert.Single(published);
        Assert.Equal((string)Customer, @event.CustomerId);
        Assert.Equal([GrantsUpdatedEvent.Topic], topics);
    }

    [Fact]
    public async Task ReplacesTheGrantsWhenTheCustomerIsAlreadyAMemberAsync()
    {
        MembersWrite.Setup(port => port.CreateAsync(It.IsAny<IMemberEntityProperties>()))
            .ReturnsAsync(MemberErrors.MemberAlreadyExists);
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(Command(disabled: true, PermissionCodes.ValidateEmailRuns));

        Assert.True(result.ShouldSucceed().Disabled);
        MembersWrite.Verify(port => port.UpdateAsync(It.Is<IMemberEntityProperties>(member =>
            member.CustomerId == Customer && member.Disabled && member.Permissions.SequenceEqual(new[] { PermissionCodes.ValidateEmailRuns }))), Times.Once);
    }

    [Fact]
    public async Task KeepsEachPermissionOnceWhenACodeIsGrantedTwiceAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(Command(disabled: false, PermissionCodes.ValidateEmailRuns, PermissionCodes.CatalogRegisterProducts, PermissionCodes.ValidateEmailRuns));

        Assert.Equal([PermissionCodes.CatalogRegisterProducts, PermissionCodes.ValidateEmailRuns], result.ShouldSucceed().Permissions);
    }

    [Fact]
    public async Task FailsWithNotFoundWithoutWritingWhenTheCustomerIsUnknownAsync()
    {
        CustomersReader.Setup(port => port.GetByIdAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(CustomerErrors.CustomerNotFound);
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(Command(disabled: false, PermissionCodes.CatalogRegisterProducts));

        result.ShouldFailWithCode(CustomerErrors.CustomerNotFound.Code);
        MembersWrite.Verify(port => port.CreateAsync(It.IsAny<IMemberEntityProperties>()), Times.Never);
        Bus.Verify(bus => bus.PublishAsync(It.IsAny<GrantsUpdatedEvent>(), It.IsAny<string[]>()), Times.Never);
    }

    [Fact]
    public async Task FailsValidationWithoutWritingWhenAPermissionIsUnknownAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(Command(disabled: false, "catalog.delete-everything"));

        result.ShouldFailWithCode(MemberErrors.PermissionUnknown);
        MembersWrite.Verify(port => port.CreateAsync(It.IsAny<IMemberEntityProperties>()), Times.Never);
    }

    [Fact]
    public async Task FailsValidationWhenTheDisabledFlagAndThePermissionsAreMissingAsync()
    {
        var facade = Build<IAuthorizationFacade>();

        var result = await facade.UpdateGrantsAsync(new UpdateGrantsCommand { CustomerId = Customer });

        Assert.Equal(
            [MemberErrors.DisabledRequired, MemberErrors.PermissionsRequired],
            result.Errors.Select(error => error.Code).Order(StringComparer.Ordinal));
    }

    private static UpdateGrantsCommand Command(bool disabled, params string[] permissions)
        => new() { CustomerId = Customer, Disabled = disabled, Permissions = permissions };
}
