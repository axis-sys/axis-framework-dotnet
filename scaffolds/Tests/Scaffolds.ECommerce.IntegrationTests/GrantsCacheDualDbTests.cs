using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.GetGrants;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.SharedKernel.Authorization;
using Scaffolds.ECommerce.SharedKernel.Authorization.Events.v1;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class GrantsCacheDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ServesTheCachedGrantsFromTheDurableTierWhenTheMembershipChangesWithoutTheGrantsEventAsync(DbProvider provider)
    {
        var customerId = await SeedCustomerAsync(provider);
        await WriteMemberAsync(provider, new FakeMember(customerId, Disabled: false, [PermissionCodes.CatalogRegisterProducts]));
        GetGrantsResponse missed;
        await using (var firstInstance = DualDbServices.BuildFullStack(fixture, provider))
            missed = await GetGrantsAsync(firstInstance, customerId);

        await WriteMemberAsync(provider, new FakeMember(customerId, Disabled: true, [PermissionCodes.CatalogRegisterProducts]));
        await using var secondInstance = DualDbServices.BuildFullStack(fixture, provider);
        var hit = await GetGrantsAsync(secondInstance, customerId);

        Assert.True(missed.IsMember);
        Assert.False(missed.IsDisabled);
        Assert.False(hit.IsDisabled);
        Assert.Equal([PermissionCodes.CatalogRegisterProducts], hit.Permissions);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ComposesTheGrantsAgainWhenTheGrantsEventEvictsTheCachedEntryAsync(DbProvider provider)
    {
        var customerId = await SeedCustomerAsync(provider);
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var cached = await GetGrantsAsync(services, customerId);

        await UpdateGrantsAsync(services, customerId, PermissionCodes.ValidateEmailRuns);
        var beforeDelivery = await GetGrantsAsync(services, customerId);
        var delivered = await OutboxDispatch.DrainCleanlyAsync(services);
        var afterDelivery = await GetGrantsAsync(services, customerId);
        await using var otherInstance = DualDbServices.BuildFullStack(fixture, provider);
        var otherInstanceAfterDelivery = await GetGrantsAsync(otherInstance, customerId);

        Assert.False(cached.IsMember);
        Assert.False(beforeDelivery.IsMember);
        Assert.True(delivered);
        Assert.True(afterDelivery.IsMember);
        Assert.Equal([PermissionCodes.ValidateEmailRuns], afterDelivery.Permissions);
        Assert.True(otherInstanceAfterDelivery.IsMember);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task DeliversTheGrantsEventWithoutEvictingWhenItsCustomerIdIsMalformedAsync(DbProvider provider)
    {
        var customerId = await SeedCustomerAsync(provider);
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var cached = await GetGrantsAsync(services, customerId);
        await WriteMemberAsync(provider, new FakeMember(customerId, Disabled: false, [PermissionCodes.CatalogRegisterProducts]));
        await PublishWithAStateChangeAsync(services, new GrantsUpdatedEvent("not-a-customer-id"));

        var delivered = await OutboxDispatch.DrainCleanlyAsync(services);
        var afterDelivery = await GetGrantsAsync(services, customerId);

        Assert.True(delivered);
        Assert.False(cached.IsMember);
        Assert.False(afterDelivery.IsMember);
    }

    private static async Task PublishWithAStateChangeAsync(ServiceProvider services, GrantsUpdatedEvent @event)
    {
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersWritePort>();
        var bus = scope.ServiceProvider.GetRequiredService<IAxisBus>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await members.CreateAsync(new FakeMember(CustomerId.New, Disabled: false, Permissions: []))).ShouldSucceed();
        (await bus.PublishAsync(@event, GrantsUpdatedEvent.Topic)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private async Task<CustomerId> SeedCustomerAsync(DbProvider provider)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Grace", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.CreateAsync(customer)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        return customer.CustomerId;
    }

    private async Task WriteMemberAsync(DbProvider provider, FakeMember member)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await members.CreateAsync(member).RecoverConflictAsync(() => members.UpdateAsync(member))).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<GetGrantsResponse> GetGrantsAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationFacade>();

        return (await authorization.GetGrantsAsync(new GetGrantsQuery { CustomerId = customerId })).ShouldSucceed();
    }

    private static async Task UpdateGrantsAsync(ServiceProvider services, CustomerId customerId, params string[] permissions)
    {
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationFacade>();

        (await authorization.UpdateGrantsAsync(new UpdateGrantsCommand { CustomerId = customerId, Disabled = false, Permissions = permissions })).ShouldSucceed();
    }
}
