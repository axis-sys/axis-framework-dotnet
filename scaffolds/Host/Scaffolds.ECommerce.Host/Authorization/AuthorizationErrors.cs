namespace Scaffolds.ECommerce.Host.Authorization;

internal static class AuthorizationErrors
{
    public static readonly AxisError IdentityRequired = AxisError.Unauthorized("AUTHORIZATION_IDENTITY_REQUIRED");
    public static readonly AxisError GrantsUnavailable = AxisError.Forbidden("AUTHORIZATION_GRANTS_UNAVAILABLE");
    public static readonly AxisError PermissionDenied = AxisError.Forbidden("AUTHORIZATION_PERMISSION_DENIED");

    public static readonly AxisError AdministratorRequired = AxisError.Forbidden("AUTHORIZATION_ADMINISTRATOR_REQUIRED");
}
