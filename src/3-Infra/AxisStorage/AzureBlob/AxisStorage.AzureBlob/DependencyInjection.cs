using Axis;
using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace AxisStorage.AzureBlob;

public static class DependencyInjection
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAxisAzureBlobStorage(AzureBlobCredentialSettings credentialSettings,
            AzureBlobSettings storageSettings,
            Action<AzureBlobStorageOptions>? configure = null)
        {
            var options = new AzureBlobStorageOptions();
            configure?.Invoke(options);
            var serviceClient = AzureBlobClients.ClientFactory(credentialSettings)(storageSettings.AccountUrl);

            services.AddSingleton(options);
            services.AddSingleton(storageSettings);
            services.AddSingleton(serviceClient);
            services.AddSingleton<AzureBlobStorageAdapter>();
            services.AddSingleton<IAxisStorage>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
            services.AddSingleton<IAxisStorageContainer>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
            services.AddSingleton<IAxisStorageLister>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
            services.AddSingleton<IAxisStorageUrlResolver>(sp => sp.GetRequiredService<AzureBlobStorageAdapter>());
            return services;
        }

        public IServiceCollection AddAxisAzureBlobStorageFactory(AzureBlobCredentialSettings credentialSettings,
            Action<AzureBlobStorageOptions>? configure = null)
        {
            var options = new AzureBlobStorageOptions();
            configure?.Invoke(options);
            var serviceClients = AzureBlobClients.ClientFactory(credentialSettings);

            services.AddSingleton(options);
            // The TokenCredential registration only exists on the AAD path — a shared-key composition
            // (emulator / key-based account) has no token credential to expose.
            if (!AzureBlobClients.HasSharedKey(credentialSettings))
                services.AddSingleton(AzureBlobCredential.Create(credentialSettings));
            services.AddSingleton<IAzureBlobStorageFactory>(sp => new AzureBlobStorageFactory(
                serviceClients,
                sp.GetRequiredService<IAxisMediatorAccessor>(),
                options));
            return services;
        }
    }
}
