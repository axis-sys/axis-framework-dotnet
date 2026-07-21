namespace Axis;

public sealed record AxisStorageUrl(string Url, bool IsPublic, DateTimeOffset? ExpiresAt);
