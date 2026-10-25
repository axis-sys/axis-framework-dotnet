using System.ComponentModel.DataAnnotations;

namespace Scaffolds.ECommerce.Adapters.Driven.AuthProviders.MsEntra;

internal sealed class MsEntraOptions
{
    public const string SectionName = "ECommerce:Auth:MsEntra";

    [Required]
    public string Instance { get; init; } = "https://login.microsoftonline.com";

    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    public string ClientId { get; init; } = string.Empty;
}
