namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeOrder(
    OrderId OrderId,
    CustomerId CustomerId,
    ProductId ProductId,
    int Quantity,
    CartId CartId
) : IOrderEntityProperties;
