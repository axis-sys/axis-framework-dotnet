using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scaffolds.ECommerce.Adapters.Driven.AuthProviders;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Application.Customers;
using Scaffolds.ECommerce.Host.Controllers.Catalog;

namespace Scaffolds.ECommerce.Host.Auth;

public static class DependencyInjection
{
    public static IServiceCollection AddECommerceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Named schemes (edge-auth-schemes): the default validates this API's own bootstrap tokens;
        // the external schemes validate the IdPs' tokens and gate only the token-exchange endpoints.
        services
            .AddAuthentication(AuthSchemes.ECommerce)
            .AddJwtBearer(AuthSchemes.ECommerce, _ => { })
            .AddJwtBearer(AuthSchemes.MsEntra, _ => { })
            .AddJwtBearer(AuthSchemes.Google, _ => { });

        services
            .AddOptions<AuthTokenOptions>()
            .Bind(configuration.GetSection(AuthTokenOptions.SectionName))
            .ValidateDataAnnotations();

        services
            .AddOptions<CustomersOptions>()
            .Bind(configuration.GetSection(CustomersOptions.SectionName));

        services
            .AddAuthProviders(configuration)
            .AddOptions<JwtBearerOptions>(AuthSchemes.ECommerce)
            .Configure<IOptions<AuthTokenOptions>>((jwt, tokenOptions) =>
            {
                SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(tokenOptions.Value.SigningKey));
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = tokenOptions.Value.Issuer,
                    ValidateAudience = true,
                    ValidAudience = tokenOptions.Value.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = AuthClaimTypes.Name,
                };
            });

        return services;
    }

    public static IServiceCollection AddECommerceAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(CatalogPolicies.Write, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(AuthClaimTypes.Permission, "admin") ||
                    context.User.HasClaim(AuthClaimTypes.Permission, CatalogPolicies.Write)));

        return services;
    }
}
