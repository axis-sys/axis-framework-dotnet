namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.Google;

public sealed class GoogleOptions
{
    public const string SectionName = "ECommerce:Auth:Google";

    public string Authority { get; init; } = "https://accounts.google.com";
    public string ClientId { get; init; } = string.Empty;
}
