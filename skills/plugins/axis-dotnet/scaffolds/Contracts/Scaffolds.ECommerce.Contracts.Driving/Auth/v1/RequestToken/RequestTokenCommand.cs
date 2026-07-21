namespace Scaffolds.ECommerce.Contracts.Driving.Auth.v1.RequestToken;

/// <summary>
/// Exchanges a valid external-provider token (Microsoft Entra, Google Accounts, ...) for a bootstrap
/// access token of this API, provisioning the customer on first sight. The identity AND the provider
/// come from the authenticated request, so the command carries no fields.
/// </summary>
public sealed record RequestTokenCommand : IAxisCommand<RequestTokenResponse>;
