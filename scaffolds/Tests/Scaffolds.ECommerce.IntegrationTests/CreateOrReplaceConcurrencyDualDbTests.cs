using System.Collections.Concurrent;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1;
using Scaffolds.ECommerce.Contracts.Driving.Authorization.v1.UpdateGrants;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;
using Scaffolds.ECommerce.SharedKernel.Authorization;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class CreateOrReplaceConcurrencyDualDbTests(DualDbFixture fixture)
{
    private const int ConcurrentWriters = 12;

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task EndsEveryGrantsUpdateInASuccessOrATypedTransientWhenConcurrentWritersReplaceTheSameMemberAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var customerId = await SeedCustomerAsync(services);
        var commands = Enumerable.Range(0, ConcurrentWriters).Select(writer => GrantsOf(customerId, writer)).ToList();

        var results = await Task.WhenAll(commands.Select(command => UpdateGrantsAsync(services, command)));

        AssertEveryOutcomeIsASuccessOrATypedTransient(results);
        var written = results.Where(result => result.IsSuccess).Select(result => result.ShouldSucceed()).Select(grants => GrantsKey(grants.Disabled, grants.Permissions));
        var member = await ReadMemberAsync(services, customerId);
        Assert.Contains(GrantsKey(member.Disabled, member.Permissions), written);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task EndsEveryCodeRequestInASuccessOrATypedTransientWhenConcurrentRequestsReplaceTheSameCodeAsync(DbProvider provider)
    {
        var sent = new ConcurrentQueue<AxisEmailData>();
        await using var services = DualDbServices.BuildFullStack(fixture, provider, RecordTheEmailsIn(sent));
        var customerId = await SeedCustomerAsync(services);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customerId.ToString();

        var results = await Task.WhenAll(Enumerable.Range(0, ConcurrentWriters).Select(_ => RequestCodeAsync(services)));

        AssertEveryOutcomeIsASuccessOrATypedTransient(results);
        var stored = await ReadCodeAsync(services, customerId);
        Assert.Contains(sent, email => email.Body.Contains(stored, StringComparison.Ordinal));
    }

    private static void AssertEveryOutcomeIsASuccessOrATypedTransient<TResponse>(AxisResult<TResponse>[] results)
    {
        Assert.Contains(results, result => result.IsSuccess);
        Assert.All(
            results.Where(result => result.IsFailure).SelectMany(result => result.Errors),
            error => Assert.Equal(AxisErrorType.ServiceUnavailable, error.Type));
    }

    private static UpdateGrantsCommand GrantsOf(CustomerId customerId, int writer) => new()
    {
        CustomerId = customerId,
        Disabled = writer % 2 == 1,
        Permissions = [.. PermissionCodes.All.Take(writer % (PermissionCodes.All.Count + 1))],
    };

    private static string GrantsKey(bool disabled, IEnumerable<string> permissions)
        => $"{disabled}:{string.Join(',', permissions)}";

    private static Action<IServiceCollection> RecordTheEmailsIn(ConcurrentQueue<AxisEmailData> sent)
        => services =>
        {
            var email = new Mock<IAxisEmailService>();
            email.Setup(service => service.SendAsync(It.IsAny<AxisEmailData>()))
                .Callback((AxisEmailData data) => sent.Enqueue(data))
                .ReturnsAsync(AxisResult.Ok());
            services.AddSingleton(email.Object);
        };

    private static async Task<CustomerId> SeedCustomerAsync(ServiceProvider services)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Racing Customer", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.CreateAsync(customer)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        return customer.CustomerId;
    }

    private static async Task<AxisResult<UpdateGrantsResponse>> UpdateGrantsAsync(ServiceProvider services, UpdateGrantsCommand command)
    {
        await Task.Yield();
        using var scope = services.CreateScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationFacade>();

        return await authorization.UpdateGrantsAsync(command);
    }

    private static async Task<AxisResult<RequestEmailValidationResponse>> RequestCodeAsync(ServiceProvider services)
    {
        await Task.Yield();
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        return await emailValidation.RequestEmailValidationAsync(new RequestEmailValidationCommand());
    }

    private static async Task<IMemberEntityProperties> ReadMemberAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var members = scope.ServiceProvider.GetRequiredService<IMembersReaderPort>();

        return (await members.GetByCustomerIdAsync(customerId)).ShouldSucceed();
    }

    private static async Task<string> ReadCodeAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesReaderPort>();

        return (await validationCodes.GetByCustomerIdAsync(customerId)).ShouldSucceed().Code;
    }
}
