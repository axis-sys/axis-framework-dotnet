namespace Scaffolds.ECommerce.IntegrationTests;

[Collection(DualDbCollection.Name)]
public sealed class CartItemsRepositoryDualDbTests(DualDbFixture fixture)
{
    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReadsBackTheCartItemWhenCreatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var cartItem = new FakeCartItem(CartId.New, ProductId.New, Quantity: 2);

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await cartItems.CreateAsync(cartItem)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsReaderPort>();
            var got = (await cartItems.GetByCartIdAsync(cartItem.CartId)).ShouldSucceed();
            Assert.Equal(cartItem.ProductId, got.ProductId);
            Assert.Equal(2, got.Quantity);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithConflictWhenTheCartAlreadyHoldsAnItemAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var cartId = CartId.New;

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await cartItems.CreateAsync(new FakeCartItem(cartId, ProductId.New, Quantity: 1))).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsWritePort>();
            AxisResult duplicate = await cartItems.CreateAsync(new FakeCartItem(cartId, ProductId.New, Quantity: 1));
            duplicate.ShouldFailWithCode(CartItemErrors.CartItemAlreadyExists.Code);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task ReplacesTheProductAndQuantityWhenTheCartItemIsUpdatedAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        var original = new FakeCartItem(CartId.New, ProductId.New, Quantity: 1);
        var replacement = original with { ProductId = ProductId.New, Quantity = 4 };

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await cartItems.CreateAsync(original)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsWritePort>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            (await cartItems.UpdateAsync(replacement)).ShouldSucceed();
            (await unitOfWork.SaveChangesAsync()).ShouldSucceed();
        }

        using (var scope = services.CreateScope())
        {
            var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsReaderPort>();
            var got = (await cartItems.GetByCartIdAsync(original.CartId)).ShouldSucceed();
            Assert.Equal(replacement.ProductId, got.ProductId);
            Assert.Equal(4, got.Quantity);
        }
    }

    [Theory]
    [InlineData(DbProvider.Postgres)]
    [InlineData(DbProvider.MySql)]
    public async Task FailsWithNotFoundWhenTheCartHoldsNoItemAsync(DbProvider provider)
    {
        await using var services = DualDbServices.Build(fixture, provider);
        using var scope = services.CreateScope();
        var cartItems = scope.ServiceProvider.GetRequiredService<ICartItemsReaderPort>();

        var result = await cartItems.GetByCartIdAsync(CartId.New);

        result.ShouldFailWithCode(CartItemErrors.CartItemNotFound.Code);
    }
}
