namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.MsEntra;

public sealed class MsEntraOptions
{
    public const string SectionName = "ECommerce:Auth:MsEntra";

    public string Instance { get; init; } = "https://login.microsoftonline.com";
    public string TenantId { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
}
