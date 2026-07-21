using Azure.Identity;

namespace AxisStorage.AzureBlob.UnitTests.AzureBlob;

public class AzureBlobCredentialTests
{
    [Fact]
    public void Create_ShouldReturnClientSecretCredential_WhenTenantClientAndSecretAreSet()
    {
        // Arrange
        var settings = new AzureBlobCredentialSettings
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret"
        };

        // Act
        var credential = AzureBlobCredential.Create(settings);

        // Assert
        Assert.IsType<ClientSecretCredential>(credential);
    }

    [Fact]
    public void Create_ShouldReturnDefaultAzureCredential_WhenOnlyManagedIdentityClientIdIsSet()
    {
        // Arrange
        var settings = new AzureBlobCredentialSettings { ManagedIdentityClientId = "managed-identity" };

        // Act
        var credential = AzureBlobCredential.Create(settings);

        // Assert
        Assert.IsType<DefaultAzureCredential>(credential);
    }

    [Fact]
    public void Create_ShouldReturnDefaultAzureCredential_WhenNoSettingsAreProvided()
    {
        // Arrange
        var settings = new AzureBlobCredentialSettings();

        // Act
        var credential = AzureBlobCredential.Create(settings);

        // Assert
        Assert.IsType<DefaultAzureCredential>(credential);
    }

    [Fact]
    public void Create_ShouldPreferClientSecretCredential_WhenBothClientSecretAndManagedIdentityAreSet()
    {
        // Arrange
        var settings = new AzureBlobCredentialSettings
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            ManagedIdentityClientId = "managed-identity"
        };

        // Act
        var credential = AzureBlobCredential.Create(settings);

        // Assert
        Assert.IsType<ClientSecretCredential>(credential);
    }

    [Fact]
    public void Create_ShouldFallBackToDefaultAzureCredential_WhenClientSecretIsIncomplete()
    {
        // Arrange: TenantId and ClientId set, but no ClientSecret
        var settings = new AzureBlobCredentialSettings { TenantId = "tenant", ClientId = "client" };

        // Act
        var credential = AzureBlobCredential.Create(settings);

        // Assert
        Assert.IsType<DefaultAzureCredential>(credential);
    }
}
