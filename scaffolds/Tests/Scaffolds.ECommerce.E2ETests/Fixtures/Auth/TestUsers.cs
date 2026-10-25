namespace Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

internal static class TestUsers
{
    public const string SystemAdminExternalId = "0192aa00-0000-7000-8000-00000000ad01";
    public const string NormalUserExternalId = "0192aa00-0000-7000-8000-0000000000b1";
    public const string StaffExternalId = "0192aa00-0000-7000-8000-0000000005f1";
    public const string DisabledStaffExternalId = "0192aa00-0000-7000-8000-0000000005f2";
    public const string NewCustomerName = "New Customer";

    public static string NewCustomerEmail(string subject) => $"{subject}@example.com";

    public static string ExternalId(EUserType user) => user switch
    {
        EUserType.SystemAdmin => SystemAdminExternalId,
        EUserType.NormalUser => NormalUserExternalId,
        EUserType.Staff => StaffExternalId,
        EUserType.DisabledStaff => DisabledStaffExternalId,
        _ => string.Empty,
    };

    public static string Email(EUserType user) => user switch
    {
        EUserType.SystemAdmin => "admin@example.com",
        EUserType.NormalUser => "customer@example.com",
        EUserType.Staff => "staff@example.com",
        EUserType.DisabledStaff => "disabled.staff@example.com",
        _ => string.Empty,
    };

    public static string Name(EUserType user) => user switch
    {
        EUserType.SystemAdmin => "System Admin",
        EUserType.NormalUser => "Normal Customer",
        EUserType.Staff => "Catalog Staff",
        EUserType.DisabledStaff => "Disabled Staff",
        _ => string.Empty,
    };
}
