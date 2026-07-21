namespace Scaffolds.ECommerce.Contracts.Driven.Auth;

public sealed record ExternalUser(string Subject, string? Email, string? Name, string Provider);
