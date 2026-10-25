namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

public static class MembersColumns
{
    public const string CustomerId = "CUSTOMER_ID";
    public const string Disabled = "DISABLED";
    public const string Permissions = "PERMISSIONS";

    public const string All = $"{CustomerId}, {Disabled}, {Permissions}";
}
