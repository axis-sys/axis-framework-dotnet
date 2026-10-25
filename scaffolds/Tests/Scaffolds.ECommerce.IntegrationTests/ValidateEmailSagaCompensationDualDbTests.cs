using Axis.SharedKernel;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class ValidateEmailSagaCompensationDualDbTests(DualDbFixture fixture)
{
    private const int MaxStatusPolls = 300;
    private static readonly TimeSpan StatusPollDelay = TimeSpan.FromMilliseconds(100);

    private static readonly string[] TerminalStatuses =
    [
        nameof(AxisSagaStatus.Completed),
        nameof(AxisSagaStatus.Compensated),
        nameof(AxisSagaStatus.Failed),
        nameof(AxisSagaStatus.Cancelled),
    ];

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task RevertsTheEmailValidationWhenTheCodeRemovalFailsAsync(DbProvider provider)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        var validationCode = new FakeValidationCode(customer.CustomerId, "pending-code");
        await SeedAsync(provider, customer, validationCode);
        await using var services = DualDbServices.BuildFullStack(fixture, provider, FailTheCodeRemoval);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customer.CustomerId.ToString();

        var sagaId = await ValidateEmailAsync(services, validationCode.Code);
        var status = await PollUntilTerminalAsync(services, sagaId);

        Assert.Equal(nameof(AxisSagaStatus.Compensated), status.Status);
        Assert.False(status.EmailValidated);
        Assert.False(await ReadEmailValidatedAsync(services, customer.CustomerId));
    }

    private static void FailTheCodeRemoval(IServiceCollection services)
    {
        var validationCodes = new Mock<IValidationCodesWritePort>();
        validationCodes.Setup(port => port.RemoveAsync(It.IsAny<CustomerId>()))
            .ReturnsAsync(ValidationCodeErrors.ValidationCodeNotFound);
        services.AddScoped(_ => validationCodes.Object);
    }

    private async Task SeedAsync(DbProvider provider, FakeCustomer customer, FakeValidationCode validationCode)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.CreateAsync(customer)).ShouldSucceed();
        (await validationCodes.CreateAsync(validationCode)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<string> ValidateEmailAsync(ServiceProvider services, string code)
    {
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        return (await emailValidation.ValidateEmailAsync(new ValidateEmailCommand { Code = code })).ShouldSucceed().SagaId;
    }

    private static async Task<GetValidationStatusResponse> PollUntilTerminalAsync(ServiceProvider services, string sagaId)
    {
        for (var poll = 0; poll < MaxStatusPolls; poll++)
        {
            using var scope = services.CreateScope();
            var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();
            var status = (await emailValidation.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = sagaId })).ShouldSucceed();
            if (TerminalStatuses.Contains(status.Status))
                return status;

            await Task.Delay(StatusPollDelay, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Saga '{sagaId}' never reached a terminal status.");
    }

    private static async Task<bool> ReadEmailValidatedAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();

        return (await customers.GetByIdAsync(customerId)).ShouldSucceed().EmailValidated;
    }
}
