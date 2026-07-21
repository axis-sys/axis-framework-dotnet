namespace AxisStorage.CloudflareR2;

public class CloudflareR2Settings
{
    public required string AccountId { get; init; }
    public required string AccessKey { get; init; }
    public required string SecretKey { get; init; }
    public required string BucketName { get; init; }
    public string? PublicUrl { get; init; }

    internal string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}