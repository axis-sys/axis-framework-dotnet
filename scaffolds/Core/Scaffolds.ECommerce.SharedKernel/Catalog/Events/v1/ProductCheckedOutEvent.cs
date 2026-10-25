namespace Scaffolds.ECommerce.SharedKernel.Catalog.Events.v1;

public sealed record ProductCheckedOutEvent(string CartId, string ProductId, int Quantity) : IAxisEvent
{
    public const string Topic = "product_checked_out";
}
