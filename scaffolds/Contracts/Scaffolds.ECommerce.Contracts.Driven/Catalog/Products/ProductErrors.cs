namespace Scaffolds.ECommerce.Contracts.Driven.Catalog.Products;

public static class ProductErrors
{
    public const string ProductIdInvalid = "PRODUCT_ID_INVALID";
    public static readonly AxisError ProductNotFound = AxisError.NotFound("PRODUCT_NOT_FOUND");

    public static readonly AxisError InsufficientStock = AxisError.BusinessRule("INSUFFICIENT_STOCK");

    public const string SkuInvalid = "SKU_INVALID";
    public const string InitialStockInvalid = "INITIAL_STOCK_INVALID";
    public static readonly AxisError SkuAlreadyExists = AxisError.Conflict("PRODUCT_SKU_ALREADY_EXISTS");
}
