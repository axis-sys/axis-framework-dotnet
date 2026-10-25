using Scaffolds.ECommerce.Contracts.Driving.Auth.v1;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;

namespace Scaffolds.ECommerce.Adapters.Driving.Facade.Auth;

internal sealed class AuthFacade(IAxisMediator mediator) : IAuthFacade
{
    public Task<AxisResult<RequestTokenResponse>> RequestTokenAsync(RequestTokenCommand command)
        => mediator.Cqrs.ExecuteAsync<RequestTokenCommand, RequestTokenResponse>(command);
}
