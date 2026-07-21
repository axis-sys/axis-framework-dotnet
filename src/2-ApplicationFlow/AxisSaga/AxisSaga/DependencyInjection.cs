using System.Reflection;
using Axis.Contracts;
using Axis.Contracts.Configuration;
using Axis.Persistence;
using Axis.Ports;
using Axis.Saga;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Axis;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the dialect-agnostic saga runtime (engine, mediator, resumer, janitor, definition
    /// initializer, stage-handler invoker, definition registry) and, when
    /// <see cref="Saga.AxisSagaSettings.ResumerEnabled"/> is set, the background resumer worker. A
    /// storage adapter (e.g. <c>AddAxisSagaPostgres</c>, <c>AddAxisSagaMySql</c>) calls this once and
    /// then supplies the four dialect store ports plus the <see cref="IAxisSagaStorageInitializer"/>.
    /// </summary>
    public static IServiceCollection AddAxisSagaCore(this IServiceCollection services, AxisSagaSettings settings)
    {
        services.AddSingleton(settings);

        services.AddSingleton<IAxisSagaDefinitionRegistry, AxisSagaDefinitionRegistry>();
        services.AddScoped<IAxisSagaMediator, SagaMediator>();
        services.AddScoped<SagaEngine>();
        services.AddScoped<ISagaStageHandlerInvoker, SagaStageHandlerInvoker>();
        services.AddScoped<IAxisSagaResumer, SagaResumer>();
        services.AddScoped<IAxisSagaJanitor, SagaJanitor>();
        services.AddScoped<IAxisSagaDefinitionInitializer, SagaDefinitionInitializer>();

        // Consumer-facing settings admin (read/adjust the global concurrency cap at runtime). Dialect-agnostic:
        // it resolves the IAxisSagaConnectionSource that the storage adapter registers and runs portable SQL.
        services.AddScoped<IAxisSagaSettingsStore, AxisSagaSettingsStore>();

        // Host the resumer loop here so consumers get crash recovery for free. Opt out via
        // AxisSagaSettings.ResumerEnabled on processes that must not run it (recovery owned elsewhere,
        // or tests with no live database).
        if (settings.ResumerEnabled)
            services.AddHostedService<AxisSagaResumerWorker>();

        return services;
    }

    /// <summary>
    /// Keyed counterpart of <see cref="AddAxisSagaCore(IServiceCollection, Saga.AxisSagaSettings)"/>: registers the
    /// SAME dialect-agnostic runtime, but every service is keyed by <paramref name="serviceKey"/> so several
    /// per-subdomain saga stores coexist in one process (mirroring the keyed <c>AddPostgresUnitOfWork</c> of
    /// AxisRepository). Each keyed service resolves its keyed dependencies by the same key; the definition
    /// registry sees only the definitions registered under this key
    /// (<c>services.AddKeyedSingleton&lt;AxisSagaDefinition&gt;(serviceKey, Define(...))</c>). Stage handlers stay
    /// unkeyed by design — the invoker matches them by (payload type, saga name, stage name). The keyed storage
    /// adapter (<c>AddAxisSagaPostgres(serviceKey, settings)</c> / <c>AddAxisSagaMySql(serviceKey, settings)</c>)
    /// calls this and supplies the four keyed store ports plus the keyed <see cref="IAxisSagaStorageInitializer"/>.
    /// </summary>
    public static IServiceCollection AddAxisSagaCore(this IServiceCollection services, string serviceKey, AxisSagaSettings settings)
    {
        services.AddKeyedSingleton(serviceKey, settings);

        services.AddKeyedSingleton<IAxisSagaDefinitionRegistry>(serviceKey,
            (sp, key) => new AxisSagaDefinitionRegistry(sp.GetKeyedServices<AxisSagaDefinition>(key)));

        // The runtime classes are reused verbatim; ActivatorUtilities resolves the unkeyed deps
        // (scope factory, loggers) from the provider and takes the keyed ones as explicit
        // args, so the key is threaded through the whole subtree without new subclasses.
        services.AddKeyedScoped<IAxisSagaMediator>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaMediator>(sp,
                sp.GetRequiredKeyedService<ISagaInstanceStore>(key),
                sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(key),
                (string)key!));

        services.AddKeyedScoped<SagaEngine>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaEngine>(sp,
                sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(key),
                sp.GetRequiredKeyedService<ISagaInstanceStore>(key),
                sp.GetRequiredKeyedService<ISagaStageLogStore>(key),
                sp.GetRequiredKeyedService<ISagaStageHandlerInvoker>(key),
                sp.GetRequiredKeyedService<AxisSagaSettings>(key)));

        services.AddKeyedScoped<ISagaStageHandlerInvoker>(serviceKey,
            (sp, _) => ActivatorUtilities.CreateInstance<SagaStageHandlerInvoker>(sp));

        services.AddKeyedScoped<IAxisSagaResumer>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaResumer>(sp,
                sp.GetRequiredKeyedService<ISagaInstanceStore>(key),
                sp.GetRequiredKeyedService<AxisSagaSettings>(key),
                sp.GetRequiredKeyedService<IAxisSagaMediator>(key)));

        services.AddKeyedScoped<IAxisSagaJanitor>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaJanitor>(sp,
                sp.GetRequiredKeyedService<ISagaInstanceStore>(key)));

        services.AddKeyedScoped<IAxisSagaDefinitionInitializer>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<SagaDefinitionInitializer>(sp,
                sp.GetRequiredKeyedService<ISagaDefinitionStore>(key),
                sp.GetRequiredKeyedService<IAxisSagaDefinitionRegistry>(key)));

        services.AddKeyedScoped<IAxisSagaSettingsStore>(serviceKey, (sp, key) =>
            ActivatorUtilities.CreateInstance<AxisSagaSettingsStore>(sp,
                sp.GetRequiredKeyedService<IAxisSagaConnectionSource>(key)));

        // One resumer worker per key. MUST be AddSingleton<IHostedService>, NOT AddHostedService<T>:
        // the latter dedups by implementation type (TryAddEnumerable), which would silently drop the
        // second key's worker (and collide with the unkeyed worker in a mixed process).
        if (settings.ResumerEnabled)
            services.AddSingleton<IHostedService>(sp =>
                ActivatorUtilities.CreateInstance<AxisSagaResumerWorker>(sp,
                    sp.GetRequiredKeyedService<IAxisSagaStorageInitializer>(serviceKey),
                    sp.GetRequiredKeyedService<AxisSagaSettings>(serviceKey),
                    serviceKey));

        return services;
    }

    public static IServiceCollection AddAxisSagaHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsGenericType: false } &&
                type.GetInterfaces().Any(i =>
                    i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IAxisSagaStageHandler<>)))
            .Distinct();

        foreach (var handlerType in handlerTypes)
        {
            var interfaces = handlerType.GetInterfaces()
                .Where(i => i.IsGenericType &&
                            i.GetGenericTypeDefinition() == typeof(IAxisSagaStageHandler<>))
                .Distinct();

            foreach (var interfaceType in interfaces)
                services.AddScoped(interfaceType, handlerType);
        }

        return services;
    }
}
