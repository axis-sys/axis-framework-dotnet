namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;

public sealed record GetOrderQuery : IAxisQuery<GetOrderResponse>
{
    [JsonIgnore]
    public string? OrderId { get; init; }
}
