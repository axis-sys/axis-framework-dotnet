using Amazon.S3;
using Amazon.S3.Model;
using Testcontainers.Minio;

namespace AxisStorage.CloudflareR2.IntegrationTests;

public sealed class MinioFixture : IAsyncLifetime
{
    private const string BucketName = "integration-test-bucket";

    private readonly MinioContainer _container = new MinioBuilder("minio/minio:RELEASE.2023-09-04T19-57-37Z")
        .Build();

    public CloudflareR2Settings Settings { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        Settings = new CloudflareR2Settings
        {
            AccountId = "localhost",
            AccessKey = _container.GetAccessKey(),
            SecretKey = _container.GetSecretKey(),
            BucketName = BucketName
        };

        using var s3Client = CreateS3Client();
        await s3Client.PutBucketAsync(new PutBucketRequest { BucketName = BucketName });
    }

    public IAmazonS3 CreateS3Client() => new AmazonS3Client(
        _container.GetAccessKey(),
        _container.GetSecretKey(),
        new AmazonS3Config
        {
            ServiceURL = _container.GetConnectionString(),
            ForcePathStyle = true
        });

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}

[CollectionDefinition("MinioCollection")]
public class MinioCollection : ICollectionFixture<MinioFixture>;
