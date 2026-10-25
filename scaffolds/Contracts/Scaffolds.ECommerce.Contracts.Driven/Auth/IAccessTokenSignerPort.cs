namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public interface IAccessTokenSignerPort
{
    AxisResult<string> Sign(AccessTokenDescriptor descriptor);
}
