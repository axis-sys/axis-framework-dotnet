namespace AxisStorage.AzureBlob;

public class AzureBlobCredentialSettings
{
    public string? TenantId { get; init; }
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string? ManagedIdentityClientId { get; init; }

    /// <summary>
    /// Storage account name for shared-key authentication. When <see cref="AccountName"/> and
    /// <see cref="AccountKey"/> are both set they take precedence over the AAD chain — the intended
    /// use is the Azurite emulator (<c>devstoreaccount1</c>) and accounts accessed by key.
    /// </summary>
    public string? AccountName { get; init; }

    /// <summary>Storage account key paired with <see cref="AccountName"/>.</summary>
    public string? AccountKey { get; init; }
}
