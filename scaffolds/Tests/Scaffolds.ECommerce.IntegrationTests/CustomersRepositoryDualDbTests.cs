namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class CustomersRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackEveryColumnWhenACustomerIsCreatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Ada", IsAdmin: true, "external-ada", "MsEntra", EmailValidated: true);
        await CreateAsync(services, customer);

        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();
        var got = (await customers.GetByIdAsync(customer.CustomerId)).ShouldSucceed();

        Assert.Equal(customer.Email, got.Email);
        Assert.Equal("Ada", got.Name);
        Assert.True(got.IsAdmin);
        Assert.Equal("external-ada", got.ExternalId);
        Assert.Equal("MsEntra", got.Provider);
        Assert.True(got.EmailValidated);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackNullsAndFalseFlagsWhenTheOptionalColumnsAreEmptyAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Grace", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        await CreateAsync(services, customer);

        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();
        var got = (await customers.GetByIdAsync(customer.CustomerId)).ShouldSucceed();

        Assert.False(got.IsAdmin);
        Assert.Null(got.ExternalId);
        Assert.Null(got.Provider);
        Assert.False(got.EmailValidated);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FindsTheCustomerWhenReadByEmailAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Alan", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        await CreateAsync(services, customer);

        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();
        var got = (await customers.GetByEmailAsync(customer.Email)).ShouldSucceed();

        Assert.Equal(customer.CustomerId, got.CustomerId);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheEmailAlreadyExistsAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var email = TestData.NewEmail();
        await CreateAsync(services, new FakeCustomer(CustomerId.New, email, "First", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false));

        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var duplicate = await customers.CreateAsync(new FakeCustomer(CustomerId.New, email, "Second", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false));

        duplicate.ShouldFailWithCode(CustomerErrors.CustomerEmailAlreadyExists.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task KeepsTwoCustomersApartWhenTheirEmailsDifferOnlyByCaseAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var lower = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Lower", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        var upper = lower with { CustomerId = CustomerId.New, Email = lower.Email.ToUpperInvariant(), Name = "Upper" };
        await CreateAsync(services, lower);
        await CreateAsync(services, upper);

        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();
        var got = (await customers.GetByEmailAsync(upper.Email)).ShouldSucceed();

        Assert.Equal(upper.CustomerId, got.CustomerId);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task PersistsTheFlagWhenTheEmailValidationIsSetThenClearedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customer = new FakeCustomer(CustomerId.New, TestData.NewEmail(), "Linus", IsAdmin: false, ExternalId: null, Provider: null, EmailValidated: false);
        await CreateAsync(services, customer);

        await SetEmailValidatedAsync(services, customer.CustomerId, emailValidated: true);
        var afterSet = await ReadEmailValidatedAsync(services, customer.CustomerId);
        await SetEmailValidatedAsync(services, customer.CustomerId, emailValidated: false);
        var afterClear = await ReadEmailValidatedAsync(services, customer.CustomerId);

        Assert.True(afterSet);
        Assert.False(afterClear);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithNotFoundWhenTheCustomerDoesNotExistAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();

        var result = await customers.GetByIdAsync(CustomerId.New);

        result.ShouldFailWithCode(CustomerErrors.CustomerNotFound.Code);
    }

    private static async Task CreateAsync(ServiceProvider services, FakeCustomer customer)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.CreateAsync(customer)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task SetEmailValidatedAsync(ServiceProvider services, CustomerId customerId, bool emailValidated)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await customers.SetEmailValidatedAsync(customerId, emailValidated)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<bool> ReadEmailValidatedAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomersReaderPort>();

        return (await customers.GetByIdAsync(customerId)).ShouldSucceed().EmailValidated;
    }
}
