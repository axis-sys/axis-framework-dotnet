namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public interface IExternalIdentityContext
{
    AxisResult<ExternalUser> Get();
}
