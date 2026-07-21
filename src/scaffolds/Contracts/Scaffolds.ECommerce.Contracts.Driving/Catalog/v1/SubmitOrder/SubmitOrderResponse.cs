namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.SubmitOrder;

/// <summary>Identifier of the submitted order.</summary>
public sealed record SubmitOrderResponse : IAxisCommandResponse
{
    /// <summary>Id of the submitted order.</summary>
    public required string OrderId { get; init; }

    /// <summary> Identifier of the product for the submitted order. </summary>
    public required string ProductId { get; init; }

    /// <summary> Quantity of the product ordered. </summary>
    public required int Quantity { get; init; }
}
