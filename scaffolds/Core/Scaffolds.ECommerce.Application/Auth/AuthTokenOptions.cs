using System.ComponentModel.DataAnnotations;

namespace Scaffolds.ECommerce.Application.Auth;

public sealed class AuthTokenOptions
{
    public const string SectionName = "ECommerce:Auth:Token";

    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    [Range(1, 86_400)]
    public int AccessTokenLifetimeSeconds { get; init; }
}
