using Azure.Storage.Blobs;
using Testcontainers.Azurite;

namespace AxisStorage.AzureBlob.IntegrationTests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string ContainerName = "integration-test-container";

    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.34.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public AzureBlobSettings Settings { get; private set; } = null!;

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Settings = new AzureBlobSettings
        {
            AccountUrl = _container.GetBlobEndpoint(),
            Container = ContainerName
        };

        var serviceClient = CreateServiceClient();
        await serviceClient.GetBlobContainerClient(ContainerName).CreateIfNotExistsAsync();
    }

    public BlobServiceClient CreateServiceClient() => new(_container.GetConnectionString());

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("AzuriteCollection")]
public class AzuriteCollection : ICollectionFixture<AzuriteFixture>;
