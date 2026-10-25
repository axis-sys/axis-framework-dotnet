using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class MembersRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackTheDisabledFlagAndThePermissionsWhenAMemberIsCreatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var member = new FakeMember(CustomerId.New, Disabled: true, [PermissionCodes.CatalogRegisterProducts, PermissionCodes.ValidateEmailRuns]);

        await CreateAsync(services, member);

        var got = await ReadAsync(services, member.CustomerId);
        Assert.True(got.Disabled);
        Assert.Equal(member.Permissions, got.Permissions);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackAnEmptyPermissionListWhenTheMemberHoldsNoneAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var member = new FakeMember(CustomerId.New, Disabled: false, Permissions: []);

        await CreateAsync(services, member);

        Assert.Empty((await ReadAsync(services, member.CustomerId)).Permissions);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheCustomerIsAlreadyAMemberAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var member = new FakeMember(CustomerId.New, Disabled: false, [PermissionCodes.CatalogRegisterProducts]);
        await CreateAsync(services, member);

        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersWritePort>();
        AxisResult duplicate = await members.CreateAsync(member with { Disabled = true });

        duplicate.ShouldFailWithCode(MemberErrors.MemberAlreadyExists.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReplacesTheGrantsWhenTheConflictingCreateIsRecoveredByAnUpdateAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var member = new FakeMember(CustomerId.New, Disabled: false, [PermissionCodes.CatalogRegisterProducts]);
        await CreateAsync(services, member);
        var replacement = member with { Disabled = true, Permissions = [PermissionCodes.ValidateEmailRuns] };

        using (var scope = services.CreateScope())
        {
            var members = scope.ServiceProvider.GetRequiredService<IMembersWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await members.CreateAsync(replacement).RecoverConflictAsync(() => members.UpdateAsync(replacement))).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        var got = await ReadAsync(services, member.CustomerId);
        Assert.True(got.Disabled);
        Assert.Equal([PermissionCodes.ValidateEmailRuns], got.Permissions);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithNotFoundWhenTheCustomerIsNotAMemberAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersReaderPort>();

        var result = await members.GetByCustomerIdAsync(CustomerId.New);

        result.ShouldFailWithCode(MemberErrors.MemberNotFound.Code);
    }

    private static async Task CreateAsync(ServiceProvider services, FakeMember member)
    {
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await members.CreateAsync(member)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<IMemberEntityProperties> ReadAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersReaderPort>();

        return (await members.GetByCustomerIdAsync(customerId)).ShouldSucceed();
    }
}
