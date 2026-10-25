namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.Orders.v1.GetOrder;

/// <summary>An order of the authenticated customer, priced and described for display.</summary>
public sealed record GetOrderResponse : IAxisQueryResponse
{
    /// <summary>Id of the order.</summary>
    public required string OrderId { get; init; }

    /// <summary>Id of the product ordered.</summary>
    public required string ProductId { get; init; }

    /// <summary>Quantity of the product ordered.</summary>
    public required int Quantity { get; init; }

    /// <summary>One line with the order total, or with the codes of why the order has no total.</summary>
    public required string Summary { get; init; }

    /// <summary>The order line and its total, or the order line with the codes of why it has no total.</summary>
    public required string Description { get; init; }
}
