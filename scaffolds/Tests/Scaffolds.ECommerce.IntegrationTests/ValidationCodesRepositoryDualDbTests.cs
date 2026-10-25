namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class ValidationCodesRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackTheCodeWhenCreatedForACustomerWithoutOneAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customerId = CustomerId.New;

        await CreateAsync(services, new FakeValidationCode(customerId, "first-code"));

        Assert.Equal("first-code", await ReadCodeAsync(services, customerId));
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheCustomerAlreadyHasACodeAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customerId = CustomerId.New;
        await CreateAsync(services, new FakeValidationCode(customerId, "first-code"));

        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();
        AxisResult duplicate = await validationCodes.CreateAsync(new FakeValidationCode(customerId, "second-code"));

        duplicate.ShouldFailWithCode(ValidationCodeErrors.ValidationCodeAlreadyExists.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReplacesThePendingCodeWhenTheConflictingCreateIsRecoveredByAnUpdateAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customerId = CustomerId.New;
        await CreateAsync(services, new FakeValidationCode(customerId, "first-code"));
        var replacement = new FakeValidationCode(customerId, "second-code");

        using (var scope = services.CreateScope())
        {
            var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await validationCodes.CreateAsync(replacement).RecoverConflictAsync(() => validationCodes.UpdateAsync(replacement))).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        Assert.Equal("second-code", await ReadCodeAsync(services, customerId));
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ForgetsTheCodeWhenItIsRemovedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var customerId = CustomerId.New;
        await CreateAsync(services, new FakeValidationCode(customerId, "code"));

        await RemoveAsync(services, customerId);

        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesReaderPort>();
        (await validationCodes.GetByCustomerIdAsync(customerId)).ShouldFailWithCode(ValidationCodeErrors.ValidationCodeNotFound.Code);
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task SucceedsWhenRemovingACodeThatDoesNotExistAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();

        var result = await validationCodes.RemoveAsync(CustomerId.New);

        result.ShouldSucceed();
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithNotFoundWhenTheCustomerHasNoCodeAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesReaderPort>();

        var result = await validationCodes.GetByCustomerIdAsync(CustomerId.New);

        result.ShouldFailWithCode(ValidationCodeErrors.ValidationCodeNotFound.Code);
    }

    private static async Task CreateAsync(ServiceProvider services, FakeValidationCode validationCode)
    {
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await validationCodes.CreateAsync(validationCode)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task RemoveAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesWritePort>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        (await validationCodes.RemoveAsync(customerId)).ShouldSucceed();
        (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
    }

    private static async Task<string> ReadCodeAsync(ServiceProvider services, CustomerId customerId)
    {
        using var scope = services.CreateScope();
        var validationCodes = scope.ServiceProvider.GetRequiredService<IValidationCodesReaderPort>();

        return (await validationCodes.GetByCustomerIdAsync(customerId)).ShouldSucceed().Code;
    }
}
