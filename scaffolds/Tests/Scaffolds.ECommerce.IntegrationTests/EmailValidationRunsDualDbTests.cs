using System.Text.Json;
using Axis.Contracts.Configuration;
using Axis.Ports;
using Axis.SharedKernel;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.Sagas.ValidateEmail;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.ValidateEmail;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class EmailValidationRunsDualDbTests(DualDbFixture fixture)
{
    private const int MaxStatusPolls = 300;
    private static readonly TimeSpan StatusPollDelay = TimeSpan.FromMilliseconds(100);

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithTheRunNotFoundWhenTheRunIsUnknownAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = NewSagaId() });

        result.ShouldFailWithCode(SharedErrors.SagaRunNotFound.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task CancelsTheRunAtOnceWhenNoRunHoldsItYetAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var sagaId = await InsertPendingRunAsync(services);
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.CancelValidationAsync(new CancelValidationCommand { SagaId = sagaId });

        Assert.Equal(nameof(AxisSagaStatus.Cancelled), result.ShouldSucceed().Status);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithTheAppNotCancellableConflictWhenTheRunAlreadyCompletedAsync(DbProvider provider)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        var validationCode = new FakeValidationCode(customer.CustomerId, "pending-code");
        await SeedAsync(provider, customer, validationCode);
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customer.CustomerId.ToString();
        var sagaId = await ValidateEmailAsync(services, validationCode.Code);
        Assert.Equal(nameof(AxisSagaStatus.Completed), (await PollUntilTerminalAsync(services, sagaId)).Status);
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.CancelValidationAsync(new CancelValidationCommand { SagaId = sagaId });

        result.ShouldFailWithCode(SharedErrors.SagaRunNotCancellable.Code);
        Assert.Equal(AxisErrorType.Conflict, Assert.Single(result.Errors).Type);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithTheRunNotFoundOnCancelWhenTheRunIsUnknownAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.CancelValidationAsync(new CancelValidationCommand { SagaId = NewSagaId() });

        result.ShouldFailWithCode(SharedErrors.SagaRunNotFound.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithTheAppStoreFailureWhenTheSagaStoreIsUnreachableAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider, sagaConnectionString: fixture.UnreachableConnectionString(provider));
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var status = await emailValidation.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = NewSagaId() });
        var cancel = await emailValidation.CancelValidationAsync(new CancelValidationCommand { SagaId = NewSagaId() });

        status.ShouldFailWithCode(SharedErrors.SagaRunStoreFailed.Code);
        cancel.ShouldFailWithCode(SharedErrors.SagaRunStoreFailed.Code);
        Assert.Equal(AxisErrorType.InternalServerError, Assert.Single(status.Errors).Type);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsToStartWithTheAppStoreFailureWhenTheSagaStoreIsUnreachableAsync(DbProvider provider)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        var validationCode = new FakeValidationCode(customer.CustomerId, "pending-code");
        await SeedAsync(provider, customer, validationCode);
        await using var services = DualDbServices.BuildFullStack(fixture, provider, sagaConnectionString: fixture.UnreachableConnectionString(provider));
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customer.CustomerId.ToString();
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.ValidateEmailAsync(new ValidateEmailCommand { Code = validationCode.Code });

        result.ShouldFailWithCode(SharedErrors.SagaRunStoreFailed.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsToStartWithTheAppRunFailureWhenTheSagaAnswersAnUnexpectedCodeAsync(DbProvider provider)
    {
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        var validationCode = new FakeValidationCode(customer.CustomerId, "pending-code");
        await SeedAsync(provider, customer, validationCode);
        await using var services = DualDbServices.BuildFullStack(fixture, provider, UnregisterTheSagaDefinitions);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customer.CustomerId.ToString();
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        var result = await emailValidation.ValidateEmailAsync(new ValidateEmailCommand { Code = validationCode.Code });

        result.ShouldFailWithCode(SharedErrors.SagaRunFailed.Code);
        Assert.Equal(AxisErrorType.InternalServerError, Assert.Single(result.Errors).Type);
    }

    private static void UnregisterTheSagaDefinitions(IServiceCollection services)
        => services.RemoveAll<AxisSagaDefinition>();

    private static string NewSagaId() => Guid.CreateVersion7().ToString();

    private static async Task<string> InsertPendingRunAsync(ServiceProvider services)
    {
        var sagaName = services.GetRequiredService<IAxisSagaDefinitionRegistry>().All
            .Single(definition => definition.PayloadType == typeof(ValidateEmailPayload)).SagaName;
        var payload = JsonSerializer.Serialize(new ValidateEmailPayload { CustomerId = CustomerId.New });
        var sagaId = NewSagaId();

        using var scope = services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ISagaInstanceStore>().InsertAsync(sagaId, sagaName, payload, retainForSeconds: null)).ShouldSucceed();
        return sagaId;
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
        string[] terminalStatuses = [nameof(AxisSagaStatus.Completed), nameof(AxisSagaStatus.Compensated), nameof(AxisSagaStatus.Failed), nameof(AxisSagaStatus.Cancelled)];
        for (var poll = 0; poll < MaxStatusPolls; poll++)
        {
            using var scope = services.CreateScope();
            var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();
            var status = (await emailValidation.GetValidationStatusAsync(new GetValidationStatusQuery { SagaId = sagaId })).ShouldSucceed();
            if (terminalStatuses.Contains(status.Status))
                return status;

            await Task.Delay(StatusPollDelay, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Saga '{sagaId}' never reached a terminal status.");
    }
}
