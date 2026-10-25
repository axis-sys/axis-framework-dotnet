using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1;
using Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.RequestEmailValidation;

namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class RequestEmailValidationDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReplacesThePendingCodeWhenTheCustomerRequestsAnotherAsync(DbProvider provider)
    {
        await using var services = DualDbServices.BuildFullStack(fixture, provider);
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        await SeedAsync(services, customer);
        services.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = (AxisEntityId)customer.CustomerId.ToString();

        await RequestAsync(services);
        var first = await ReadCodeAsync(services, customer.CustomerId);
        await RequestAsync(services);
        var second = await ReadCodeAsync(services, customer.CustomerId);

        Assert.NotEqual(first, second);
    }

    private static async Task SeedAsync(ServiceProvider services, FakeCustomer customer)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.CreateAsync(customer)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task RequestAsync(ServiceProvider services)
    {
        using var scope = services.CreateScope();
        var emailValidation = scope.ServiceProvider.GetRequiredService<IEmailValidationFacade>();

        (await emailValidation.RequestEmailValidationAsync(new RequestEmailValidationCommand())).ShouldSucceed();
    }

    private static async Task<string> ReadCodeAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesReaderPort>();

        return (await validationCodes.GetByCustomerIdAsync(customerId)).ShouldSucceed().Code;
    }
}
