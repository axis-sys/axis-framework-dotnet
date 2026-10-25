namespace Scaffolds.ECommerce.IntegrationTests;

internal sealed record FakeValidationCode(
    CustomerId CustomerId,
    string Code
) : IValidationCodeEntityProperties;
