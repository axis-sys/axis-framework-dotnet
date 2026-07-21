namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Auth.ValidationCodes;

public static class ValidationCodesColumns
{
    public const string CustomerId = "CUSTOMER_ID";
    public const string Code = "CODE";
    public const string All = $"{CustomerId}, {Code}";
}
