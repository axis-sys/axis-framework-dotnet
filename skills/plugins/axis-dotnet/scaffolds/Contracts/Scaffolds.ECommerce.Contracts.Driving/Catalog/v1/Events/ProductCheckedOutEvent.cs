namespace Scaffolds.ECommerce.Contracts.Driving.Catalog.v1.Events;

/// <summary>
/// Raised after a checkout reserves stock for a product, so a consumer can associate the product with the
/// cart it was checked out into. Carries identifiers only — no pricing, no product data.
/// </summary>
/// <param name="CartId">Id of the cart the product was checked out into.</param>
/// <param name="ProductId">Id of the product whose stock was reserved.</param>
/// <param name="Quantity">Number of units reserved.</param>
public sealed record ProductCheckedOutEvent(string CartId, string ProductId, int Quantity) : IAxisEvent
{
    public const string Topic = "product_checked_out";
}
