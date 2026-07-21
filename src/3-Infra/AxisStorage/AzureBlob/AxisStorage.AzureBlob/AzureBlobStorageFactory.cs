using System.Collections.Concurrent;
using Axis;
using AxisMediator.Contracts;
using Azure.Storage.Blobs;

namespace AxisStorage.AzureBlob;

internal sealed class AzureBlobStorageFactory(
    Func<string, BlobServiceClient> serviceClients,
    IAxisMediatorAccessor accessor,
    AzureBlobStorageOptions options
) : IAzureBlobStorageFactory
{
    private readonly ConcurrentDictionary<string, AzureBlobStorageAdapter> _adapters = new();

    public IAxisStorage Create(AzureBlobSettings destination)
        => _adapters.GetOrAdd($"{destination.AccountUrl}/{destination.Container}", _ => new AzureBlobStorageAdapter(
            serviceClients(destination.AccountUrl),
            destination,
            accessor,
            options));
}
