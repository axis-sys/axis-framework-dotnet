namespace Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;

public static class MemberErrors
{
    public static readonly AxisError MemberNotFound = AxisError.NotFound("MEMBER_NOT_FOUND");

    public const string DisabledRequired = "MEMBER_DISABLED_REQUIRED";
    public const string PermissionsRequired = "MEMBER_PERMISSIONS_REQUIRED";
    public const string PermissionUnknown = "PERMISSION_UNKNOWN";
    public static readonly AxisError MemberAlreadyExists = AxisError.Conflict("MEMBER_ALREADY_EXISTS");

    public static readonly AxisError GrantsUpdatedEventMalformed = AxisError.ValidationRule("GRANTS_UPDATED_EVENT_MALFORMED");
}
