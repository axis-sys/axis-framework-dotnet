using AxisBus.MySql;
using AxisBus.Postgres;
using AxisBus.Repository;
using AxisMediator.Contracts;
using AxisRepository.MySql;
using AxisRepository.Postgres;
using Axis.Saga;
using AxisSaga.MySql;
using AxisSaga.MySql.Persistence;
using AxisSaga.Postgres;
using AxisSaga.Postgres.Persistence;
using Scaffolds.ECommerce.Adapters.Driven.InMemory;
using Scaffolds.ECommerce.Adapters.Driven.MySql;
using Scaffolds.ECommerce.Adapters.Driven.Postgres;
using Scaffolds.ECommerce.Adapters.Driven.Repository;
using Scaffolds.ECommerce.Adapters.Driving.Facade;
using Scaffolds.ECommerce.Application;
using Scaffolds.ECommerce.Application.Auth;
using Scaffolds.ECommerce.Host.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
    })
    .AddMvc();

var connectionString = builder.Configuration.GetConnectionString("ECommerce")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:ECommerce.");
// The domain repository is swappable infra (architecture-swappable-infra-ports): Postgres and MySql run
// the exact same repository/handler code, chosen at boot by Database:Provider (default Postgres). The
// E2E suite pins this per Testcontainers coverage class (testing-e2e-hermetic-provider-pinning).
var useMySql = string.Equals(builder.Configuration["Database:Provider"], "MySql", StringComparison.OrdinalIgnoreCase);

builder.Services.AddAxisLogger();
// The real repository/unit-of-work adapters resolve IAxisTelemetry unconditionally; the scaffold has no
// tracing backend wired, so the no-op implementation stands in (swap for AxisTelemetry.AzureMonitor/OpenTelemetry to enable one).
builder.Services.AddSingleton<IAxisTelemetry>(NullAxisTelemetry.Instance);
builder.Services.AddECommerceApplication();
builder.Services.AddECommerceFacade();

// Products/Orders/Customers/CartItems/ValidationCodes: the real repository. Registered before AddAxisBusX,
// whose outbox override must land after this unit of work registration (AxisBus.Postgres/MySql — see DependencyInjection.cs).
// The saga runtime shares the same physical database (its own AXIS_SAGA schema) and ResumerEnabled defaults
// to true, so AddAxisSagaPostgres/MySql also registers the hosted AxisSagaResumerWorker — the saga now runs
// via that background worker (schema bootstrap, definition catalogue, crash recovery), not bare in-process.
if (useMySql)
{
    builder.Services.AddECommerceMySql(connectionString);
    builder.Services.AddAxisBusMySql(new AxisBusRepositorySettings { ConnectionString = connectionString });
    builder.Services.AddAxisSagaMySql(new AxisSagaSettings { ConnectionString = connectionString });
}
else
{
    builder.Services.AddECommercePostgres(connectionString);
    builder.Services.AddAxisBusPostgres(new AxisBusRepositorySettings { ConnectionString = connectionString });
    builder.Services.AddAxisSagaPostgres(new AxisSagaSettings { ConnectionString = connectionString });
}

// The email outbox is the only adapter left in-memory (a test double, not business state).
builder.Services.AddInMemoryEmail();

builder.Services.AddECommerceAuthentication(builder.Configuration);
builder.Services.AddECommerceAuthorization();

var app = builder.Build();

// The saga schema is also self-initialized by AxisSagaResumerWorker on its first pass, but that runs as a
// background IHostedService with no guaranteed ordering against the first request — migrate explicitly here
// too (idempotent) so AXIS_SAGA exists deterministically before the app starts serving traffic, exactly like
// the domain schema below.
if (useMySql)
{
    await EComMigrations.InitializeAsync(connectionString, new MySqlSqlDialect(), new MySqlMigrationRunner());
    await AxisSagaMySqlMigrations.InitializeMySqlAsync(connectionString);
}
else
{
    await EComMigrations.InitializeAsync(connectionString, new PostgresSqlDialect(), new PostgresMigrationRunner());
    await AxisSagaMigrations.InitializePostgresAsync(connectionString);
}

app.UseAuthentication();

app.Use(async (context, next) =>
{
    var subject = context.User.FindFirst(AuthClaimTypes.CustomerId)?.Value;
    if (subject is not null && AxisEntityId.TryParse(subject, out var entityId))
        context.RequestServices.GetRequiredService<IAxisMediatorContextAccessor>().AxisEntityId = entityId;

    await next();
});

app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed so the end-to-end project can boot the real pipeline via WebApplicationFactory<Program>.
namespace Scaffolds.ECommerce.Host
{
    public partial class Program;
}
