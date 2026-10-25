namespace Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;

/// <summary>Bootstrap access token issued from an authenticated external identity.</summary>
public sealed record RequestTokenResponse : IAxisCommandResponse
{
    /// <summary>The signed bootstrap access token (JWT).</summary>
    public required string AccessToken { get; init; }

    /// <summary>Instant the access token expires (UTC).</summary>
    public required DateTimeOffset AccessTokenExpiresAt { get; init; }

    /// <summary>Id of the customer the token belongs to.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Whether the customer's email is already validated.</summary>
    public required bool EmailValidated { get; init; }
}
