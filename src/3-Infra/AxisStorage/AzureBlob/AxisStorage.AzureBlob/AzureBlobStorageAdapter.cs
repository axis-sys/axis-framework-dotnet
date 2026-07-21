using Axis;
using AxisMediator.Contracts;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace AxisStorage.AzureBlob;

internal sealed class AzureBlobStorageAdapter(
    BlobServiceClient serviceClient,
    AzureBlobSettings settings,
    IAxisMediatorAccessor accessor,
    AzureBlobStorageOptions options
) : IAxisStorage, IAxisStorageContainer, IAxisStorageLister, IAxisStorageUrlResolver
{
    private static readonly TimeSpan DelegationKeyLifetime = TimeSpan.FromMinutes(50);

    private readonly SemaphoreSlim _delegationKeyLock = new(1, 1);
    private (UserDelegationKey Key, DateTimeOffset ExpiresOn)? _delegationKey;

    private readonly SemaphoreSlim _publicAccessLock = new(1, 1);
    private (bool IsPublic, DateTimeOffset ExpiresOn)? _publicAccess;

    public Task<AxisResult> UploadAsync(string key, Stream content, string contentType)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            if (content.CanSeek)
                content.Position = 0;
            await AzureBlobRetry.ExecuteAsync(() => BlobClient(key).UploadAsync(content, new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            }, ct), ct);
        });

    public Task<AxisResult<Stream>> DownloadAsync(string key)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            var response = await AzureBlobRetry.ExecuteAsync(() => BlobClient(key).DownloadStreamingAsync(new BlobDownloadOptions(), ct), ct);
            return response.Value.Content;
        });

    public Task<AxisResult> DeleteAsync(string key)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            await AzureBlobRetry.ExecuteAsync(() => BlobClient(key).DeleteIfExistsAsync(cancellationToken: ct), ct);
        });

    public Task<AxisResult<bool>> ExistsAsync(string key)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            var response = await AzureBlobRetry.ExecuteAsync(() => BlobClient(key).ExistsAsync(ct), ct);
            return response.Value;
        });

    public Task<AxisResult<string>> GetPresignedUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            return await BuildSasUrlAsync(key, expiration, ct);
        });

    public Task<AxisResult<AxisStorageUrl>> GetServableUrlAsync(string key, TimeSpan expiration)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            var isPublic = await IsPublicCachedAsync(ct);
            if (isPublic)
                return new AxisStorageUrl(BlobClient(key).Uri.ToString(), IsPublic: true, ExpiresAt: null);

            var url = await BuildSasUrlAsync(key, expiration, ct);
            return new AxisStorageUrl(url, IsPublic: false, ExpiresAt: DateTimeOffset.UtcNow.Add(expiration));
        });

    public Task<AxisResult<bool>> ExistsAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            var response = await AzureBlobRetry.ExecuteAsync(() => ContainerClient().ExistsAsync(ct), ct);
            return response.Value;
        });

    public Task<AxisResult> EnsureExistsAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            await AzureBlobRetry.ExecuteAsync(() => ContainerClient().CreateIfNotExistsAsync(cancellationToken: ct), ct);
        });

    public Task<AxisResult<bool>> IsPubliclyAccessibleAsync()
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            return await AzureBlobRetry.ExecuteAsync(() => ProbePublicAsync(ct), ct);
        });

    public Task<AxisResult<IReadOnlyList<string>>> ListAsync(string prefix)
        => AxisResult.TryAsync(async () =>
        {
            var ct = CancellationToken();
            List<string> keys = [];
            await foreach (var blob in ContainerClient().GetBlobsAsync(BlobTraits.None, BlobStates.None, prefix, ct))
                keys.Add(blob.Name);
            return (IReadOnlyList<string>)keys;
        });

    private CancellationToken CancellationToken()
    {
        var ct = accessor.AxisMediator!.CancellationToken;
        ct.ThrowIfCancellationRequested();
        return ct;
    }

    private BlobContainerClient ContainerClient() => serviceClient.GetBlobContainerClient(settings.Container);

    private BlobClient BlobClient(string key) => ContainerClient().GetBlobClient(key);

    private async Task<string> BuildSasUrlAsync(string key, TimeSpan expiration, CancellationToken ct)
    {
        var blob = BlobClient(key);

        // A shared-key client (emulator / key-based account) signs its own SAS; only AAD clients need
        // the user-delegation key round trip.
        if (blob.CanGenerateSasUri)
            return blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(expiration)).ToString();

        var delegationKey = await AzureBlobRetry.ExecuteAsync(() => DelegationKeyAsync(ct), ct);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = settings.Container,
            BlobName = key,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiration)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sas = sasBuilder.ToSasQueryParameters(delegationKey, serviceClient.AccountName);
        return new UriBuilder(blob.Uri) { Query = sas.ToString() }.Uri.ToString();
    }

    private async Task<bool> ProbePublicAsync(CancellationToken ct)
    {
        var properties = await ContainerClient().GetPropertiesAsync(cancellationToken: ct);
        return properties.Value.PublicAccess is not null and not PublicAccessType.None;
    }

    private async Task<bool> IsPublicCachedAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_publicAccess is { } cached && cached.ExpiresOn > now)
            return cached.IsPublic;

        await _publicAccessLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_publicAccess is { } cachedAfterLock && cachedAfterLock.ExpiresOn > now)
                return cachedAfterLock.IsPublic;

            var isPublic = await AzureBlobRetry.ExecuteAsync(() => ProbePublicAsync(ct), ct);
            _publicAccess = (isPublic, now.Add(options.PublicAccessCacheTtl));
            return isPublic;
        }
        finally
        {
            _publicAccessLock.Release();
        }
    }

    private async Task<UserDelegationKey> DelegationKeyAsync(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        if (_delegationKey is { } cached && cached.ExpiresOn > now)
            return cached.Key;

        await _delegationKeyLock.WaitAsync(ct);
        try
        {
            now = DateTimeOffset.UtcNow;
            if (_delegationKey is { } cachedAfterLock && cachedAfterLock.ExpiresOn > now)
                return cachedAfterLock.Key;

            var expiresOn = now.Add(DelegationKeyLifetime);
            var response = await serviceClient.GetUserDelegationKeyAsync(now, expiresOn, ct);
            _delegationKey = (response.Value, expiresOn);
            return response.Value;
        }
        finally
        {
            _delegationKeyLock.Release();
        }
    }
}
