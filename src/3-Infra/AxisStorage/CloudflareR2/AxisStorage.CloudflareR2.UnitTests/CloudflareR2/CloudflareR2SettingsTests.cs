namespace AxisStorage.CloudflareR2.UnitTests.CloudflareR2;

public class CloudflareR2SettingsTests
{
    [Fact]
    public void ServiceUrl_ShouldBuildCorrectEndpoint()
    {
        // Arrange
        var settings = new CloudflareR2Settings
        {
            AccountId = "my-account-id",
            AccessKey = "key",
            SecretKey = "secret",
            BucketName = "bucket"
        };

        // Act & Assert
        Assert.Equal("https://my-account-id.r2.cloudflarestorage.com", settings.ServiceUrl);
    }

    [Fact]
    public void Settings_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var settings = new CloudflareR2Settings
        {
            AccountId = "acc",
            AccessKey = "access",
            SecretKey = "secret",
            BucketName = "bucket",
            PublicUrl = "https://cdn.example.com"
        };

        // Assert
        Assert.Equal("acc", settings.AccountId);
        Assert.Equal("access", settings.AccessKey);
        Assert.Equal("secret", settings.SecretKey);
        Assert.Equal("bucket", settings.BucketName);
        Assert.Equal("https://cdn.example.com", settings.PublicUrl);
    }

    [Fact]
    public void PublicUrl_ShouldBeNullByDefault()
    {
        // Arrange & Act
        var settings = new CloudflareR2Settings
        {
            AccountId = "acc",
            AccessKey = "access",
            SecretKey = "secret",
            BucketName = "bucket"
        };

        // Assert
        Assert.Null(settings.PublicUrl);
    }
}
