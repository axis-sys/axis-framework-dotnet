using Azure.Storage;
using Azure.Storage.Blobs;

namespace AxisStorage.AzureBlob;

/// <summary>
/// Builds <see cref="BlobServiceClient"/>s from one credential decision: shared key when
/// <see cref="AzureBlobCredentialSettings.AccountName"/>/<see cref="AzureBlobCredentialSettings.AccountKey"/>
/// are present (emulator / key-based accounts), otherwise the AAD chain (<see cref="AzureBlobCredential"/>).
/// The credential is created once and reused across every account URL the returned factory serves.
/// </summary>
internal static class AzureBlobClients
{
    public static bool HasSharedKey(AzureBlobCredentialSettings settings)
        => !string.IsNullOrWhiteSpace(settings.AccountName) && !string.IsNullOrWhiteSpace(settings.AccountKey);

    public static Func<string, BlobServiceClient> ClientFactory(AzureBlobCredentialSettings settings)
    {
        if (HasSharedKey(settings))
        {
            var sharedKey = new StorageSharedKeyCredential(settings.AccountName, settings.AccountKey);
            return accountUrl => new BlobServiceClient(new Uri(accountUrl), sharedKey);
        }

        var token = AzureBlobCredential.Create(settings);
        return accountUrl => new BlobServiceClient(new Uri(accountUrl), token);
    }
}
