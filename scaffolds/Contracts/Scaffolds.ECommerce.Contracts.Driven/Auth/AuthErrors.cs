namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public static class AuthErrors
{
    public static readonly AxisError ExternalTokenInvalid = AxisError.Unauthorized("EXTERNAL_TOKEN_INVALID");
}
