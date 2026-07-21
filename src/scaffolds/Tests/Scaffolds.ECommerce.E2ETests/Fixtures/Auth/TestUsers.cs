namespace Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

public static class TestUsers
{
    public const string SystemAdminExternalId = "0192aa00-0000-7000-8000-00000000ad01";
    public const string NormalUserExternalId = "0192aa00-0000-7000-8000-0000000000b1";

    public static string ExternalId(EUserType user) => user switch
    {
        EUserType.SystemAdmin => SystemAdminExternalId,
        EUserType.NormalUser => NormalUserExternalId,
        _ => string.Empty,
    };

    public static string Email(EUserType user) => user switch
    {
        EUserType.SystemAdmin => "admin@example.com",
        EUserType.NormalUser => "customer@example.com",
        _ => string.Empty,
    };

    public static string Name(EUserType user) => user switch
    {
        EUserType.SystemAdmin => "System Admin",
        EUserType.NormalUser => "Normal Customer",
        _ => string.Empty,
    };
}
