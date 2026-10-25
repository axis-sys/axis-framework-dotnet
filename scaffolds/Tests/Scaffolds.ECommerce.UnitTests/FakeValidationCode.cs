namespace Scaffolds.ECommerce.UnitTests;

internal sealed record FakeValidationCode(
    CustomerId CustomerId,
    string Code
) : IValidationCodeEntityProperties;
