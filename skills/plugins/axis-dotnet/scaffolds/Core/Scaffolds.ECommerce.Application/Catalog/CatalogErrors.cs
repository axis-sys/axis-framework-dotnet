namespace Scaffolds.ECommerce.Application.Catalog;

internal static class CatalogErrors
{
    // Shared
    public const string ProductIdInvalid = "PRODUCT_ID_INVALID";
    public const string CartIdInvalid = "CART_ID_INVALID";
    public const string QuantityMustBePositive = "QUANTITY_MUST_BE_POSITIVE";

    // Checkout
    public const string InsufficientStock = "INSUFFICIENT_STOCK";

    // RegisterProduct
    public const string SkuInvalid = "SKU_INVALID";
    public const string NameRequired = "NAME_REQUIRED";
    public const string InitialStockInvalid = "INITIAL_STOCK_INVALID";

    // CreateProduct (strict create — a duplicate SKU is a conflict, not an idempotent no-op)
    public const string SkuAlreadyExists = "PRODUCT_SKU_ALREADY_EXISTS";
}
