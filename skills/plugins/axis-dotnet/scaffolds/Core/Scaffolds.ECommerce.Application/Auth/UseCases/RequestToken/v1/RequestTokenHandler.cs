using Scaffolds.ECommerce.Application.Auth.Services.AuthTokenIssuer;
using Scaffolds.ECommerce.Contracts.Driven.Auth;
using Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1;
using Scaffolds.ECommerce.Contracts.Driving.Customers.v1.EnsureCustomer;
using Scaffolds.ECommerce.SharedKernel.ValueObjects;

namespace Scaffolds.ECommerce.Application.Auth.UseCases.RequestToken.v1;

internal sealed class RequestTokenHandler(
    IExternalIdentityContext externalIdentity,
    ICustomersFacade customersFacade,
    IAuthTokenIssuerService tokenIssuerService
) : IAxisCommandHandler<RequestTokenCommand, RequestTokenResponse>
{
    public Task<AxisResult<RequestTokenResponse>> HandleAsync(RequestTokenCommand command)
        => externalIdentity
            .Get()
            .ThenAsync(user => customersFacade.EnsureCustomerAsync(
                    new EnsureCustomerCommand
                    {
                        ExternalId = user.Subject,
                        Email = user.Email,
                        Name = user.Name,
                        Provider = user.Provider
                    })
                )
            .ZipAsync(customer => tokenIssuerService.IssueBootstrapToken(
                new User(
                    UserId: customer.CustomerId,
                    Email: customer.Email,
                    Name: customer.Name,
                    Provider: customer.Provider,
                    ExternalId: customer.ExternalId,
                    IsSystemAdim: customer.IsAdmin
                )))
            .MapAsync((customer, token) => new RequestTokenResponse
            {
                AccessToken = token.AccessToken,
                AccessTokenExpiresAt = token.ExpiresAt,
                CustomerId = customer.CustomerId,
                EmailValidated = customer.EmailValidated,
            });
}
