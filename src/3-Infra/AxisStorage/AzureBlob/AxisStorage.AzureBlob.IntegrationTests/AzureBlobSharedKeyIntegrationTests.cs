using AxisMediator.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AxisStorage.AzureBlob.IntegrationTests;

/// <summary>
/// Exercises the shared-key composition path (AccountName/AccountKey on
/// <see cref="AzureBlobCredentialSettings"/>) end-to-end against Azurite: the factory registration,
/// the per-destination client creation and the self-signed SAS branch — the same wiring an
/// application uses to point the factory at the emulator instead of an AAD-authenticated account.
/// </summary>
[Collection("AzuriteCollection")]
public class AzureBlobSharedKeyIntegrationTests(AzuriteFixture fixture)
{
    private static readonly HttpClient Http = new();
    private static string UniqueKey() => $"shared-key/{Guid.NewGuid():N}.txt";

    private static string ConnectionStringPart(string connectionString, string name)
        => connectionString.Split(';')
            .Single(part => part.StartsWith($"{name}=", StringComparison.OrdinalIgnoreCase))[$"{name}=".Length..];

    [Fact]
    public async Task Factory_with_shared_key_uploads_and_downloads_through_the_real_adapter()
    {
        var storage = CreateFactoryStorage();
        var key = UniqueKey();
        using var stream = new MemoryStream("shared-key bytes"u8.ToArray());

        (await storage.UploadAsync(key, stream, "text/plain")).ShouldSucceed();

        var download = await storage.DownloadAsync(key);
        using var reader = new StreamReader(download.ShouldSucceed());
        Assert.Equal("shared-key bytes", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Shared_key_client_signs_its_own_sas_and_the_url_serves_the_blob()
    {
        var storage = CreateFactoryStorage();
        var key = UniqueKey();
        using var stream = new MemoryStream("sas bytes"u8.ToArray());
        (await storage.UploadAsync(key, stream, "text/plain")).ShouldSucceed();

        var url = (await storage.GetPresignedUrlAsync(key, TimeSpan.FromMinutes(5))).ShouldSucceed();

        var served = await Http.GetStringAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal("sas bytes", served);
    }

    private IAxisStorage CreateFactoryStorage()
    {
        var accessorMock = new Mock<IAxisMediatorAccessor>();
        var mediatorMock = new Mock<IAxisMediator>();
        mediatorMock.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        accessorMock.SetupGet(x => x.AxisMediator).Returns(mediatorMock.Object);

        var services = new ServiceCollection();
        services.AddSingleton(accessorMock.Object);
        services.AddAxisAzureBlobStorageFactory(new AzureBlobCredentialSettings
        {
            AccountName = ConnectionStringPart(fixture.ConnectionString, "AccountName"),
            AccountKey = ConnectionStringPart(fixture.ConnectionString, "AccountKey"),
        });

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IAzureBlobStorageFactory>();
        return factory.Create(fixture.Settings);
    }
}
