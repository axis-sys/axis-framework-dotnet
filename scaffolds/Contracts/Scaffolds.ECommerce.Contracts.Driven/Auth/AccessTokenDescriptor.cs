namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public sealed record AccessTokenDescriptor(IReadOnlyDictionary<string, string> Claims, DateTimeOffset IssuedAt, DateTimeOffset ExpiresAt);
