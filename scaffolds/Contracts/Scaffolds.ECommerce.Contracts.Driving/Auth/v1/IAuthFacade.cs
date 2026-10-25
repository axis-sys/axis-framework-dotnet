using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;

namespace Scaffolds.ECommerce.Contracts.Driving.Auth.v1;

public interface IAuthFacade
{
    Task<AxisResult<RequestTokenResponse>> RequestTokenAsync(RequestTokenCommand command);
}
