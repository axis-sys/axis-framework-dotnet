using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scaffolds.ECommerce.Adapters.Driven.AuthProviders;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Application.Customers;
using Scaffolds.ECommerce.SharedKernel.Auth;

namespace Scaffolds.ECommerce.Host.Auth;

internal static class DependencyInjection
{
    internal static IServiceCollection AddECommerceAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddAuthentication(AuthSchemes.ECommerce)
            .AddJwtBearer(AuthSchemes.ECommerce, _ => { })
            .AddJwtBearer(AuthSchemes.MsEntra, _ => { })
            .AddJwtBearer(AuthSchemes.Google, _ => { });

        services
            .AddOptions<AuthTokenOptions>()
            .Bind(configuration.GetSection(AuthTokenOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

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
}
