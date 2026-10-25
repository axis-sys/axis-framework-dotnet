using System.ComponentModel.DataAnnotations;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.Google;

internal sealed class GoogleOptions
{
    public const string SectionName = "ECommerce:Auth:Google";

    [Required]
    public string Authority { get; init; } = "https://accounts.google.com";

    [Required]
    public string ClientId { get; init; } = string.Empty;
}
