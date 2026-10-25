using Scaffolds.ECommerce.Adapters.Driven.InMemory;
using Scaffolds.ECommerce.Adapters.Driven.Jwt;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using Scaffolds.ECommerce.Contracts.Driving.Catalog.Products.v1;
using Scaffolds.ECommerce.Host.Auth;
using Scaffolds.ECommerce.Host.Authorization;
using Scaffolds.ECommerce.Host.Catalog;
using Scaffolds.ECommerce.Host.OpenApi;
using Scaffolds.ECommerce.Host.Persistence;
using Scaffolds.ECommerce.SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddAxisUrlVersioning()
    .AddAxisOpenApi(ECommerceOpenApi.Configure, typeof(Program).Assembly, typeof(IProductsFacade).Assembly);

builder.Services.AddAxisLogger();
builder.Services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
builder.Services.AddECommerceApplication();
builder.Services.AddECommerceFacade();
builder.Services.AddECommercePersistence(builder.Configuration);
builder.Services.AddInMemoryEmail();
builder.Services.AddJwtAccessTokens();

builder.Services.AddECommerceAuthentication(builder.Configuration);
builder.Services.AddECommerceAuthorization();
builder.Services.AddECommerceCatalog(builder.Configuration);

var app = builder.Build();

if (MigrateCommand.IsRequested(args))
    return await MigrateCommand.RunAsync(app);

app.UseAuthentication();
app.UseAxisAmbientIdentity(AuthClaimTypes.CustomerId);
app.UseAuthorization();
app.MapControllers();
app.MapAxisOpenApi(configure: null, AuthSchemes.ECommerce);

await app.RunAsync();
return 0;

namespace Scaffolds.ECommerce.Host
{
    public partial class Program;
}
