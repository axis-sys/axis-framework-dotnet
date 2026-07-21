using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Scaffolds.ECommerce.Adapters.Driven.AuthProviders.Google;
using Scaffolds.ECommerce.Adapters.Driven.AuthProviders.MsEntra;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Contracts.Driven.Auth;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders;

public static class DependencyInjection
{
    public static IServiceCollection AddAuthProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IExternalIdentityContext, HttpExternalIdentityContext>();

        services
            .AddOptions<MsEntraOptions>()
            .Bind(configuration.GetSection(MsEntraOptions.SectionName));

        services
            .AddOptions<GoogleOptions>()
            .Bind(configuration.GetSection(GoogleOptions.SectionName));

        services
            .AddOptions<JwtBearerOptions>(AuthSchemes.MsEntra)
            .Configure<IOptions<MsEntraOptions>>((jwt, msEntra) =>
            {
                jwt.Authority = $"{msEntra.Value.Instance}/{msEntra.Value.TenantId}/v2.0";
                jwt.Audience = msEntra.Value.ClientId;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "name";
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity)
                            MsEntraClaimMapper.MapCustomClaims(identity);

                        return Task.CompletedTask;
                    },
                };
            });

        services
            .AddOptions<JwtBearerOptions>(AuthSchemes.Google)
            .Configure<IOptions<GoogleOptions>>((jwt, google) =>
            {
                jwt.Authority = google.Value.Authority;
                jwt.Audience = google.Value.ClientId;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters.NameClaimType = "name";
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity)
                            GoogleClaimMapper.MapCustomClaims(identity);

                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }
}
