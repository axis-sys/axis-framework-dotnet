using Azure.Core;
using Azure.Identity;

namespace AxisStorage.AzureBlob;

public static class AzureBlobCredential
{
    public static TokenCredential Create(AzureBlobCredentialSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.TenantId)
            && !string.IsNullOrWhiteSpace(settings.ClientId)
            && !string.IsNullOrWhiteSpace(settings.ClientSecret))
            return new ClientSecretCredential(settings.TenantId, settings.ClientId, settings.ClientSecret);

        if (!string.IsNullOrWhiteSpace(settings.ManagedIdentityClientId))
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = settings.ManagedIdentityClientId
            });

        return new DefaultAzureCredential();
    }
}
