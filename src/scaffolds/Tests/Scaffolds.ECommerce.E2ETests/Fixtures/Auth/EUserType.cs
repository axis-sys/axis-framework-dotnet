namespace Scaffolds.ECommerce.E2ETests.Fixtures.Auth;

public enum EUserType
{
    /// <summary>Authenticated customer with no special permissions (the 403 caller).</summary>
    NormalUser,

    /// <summary>External id listed in BootstrapAdminExternalIds, promoted to admin on first login.</summary>
    SystemAdmin,

    /// <summary>Authenticates but carries no usable subject claim (exercises the 401 inside the exchange).</summary>
    NoSubjectUser,
}
