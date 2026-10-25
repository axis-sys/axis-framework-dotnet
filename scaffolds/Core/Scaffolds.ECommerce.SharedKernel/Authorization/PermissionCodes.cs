namespace Scaffolds.ECommerce.SharedKernel.Authorization;

public static class PermissionCodes
{
    public const string CatalogRegisterProducts = "catalog.register-products";
    public const string ValidateEmailRuns = "email-validation.validate-email-runs";

    public static IReadOnlyList<string> All { get; } = [CatalogRegisterProducts, ValidateEmailRuns];
}
