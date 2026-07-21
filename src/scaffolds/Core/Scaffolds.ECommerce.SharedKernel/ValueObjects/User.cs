namespace Scaffolds.ECommerce.SharedKernel.ValueObjects;

public sealed record User(
AxisEntityId UserId,
string Email,
string Name,
string? ExternalId,
string? Provider,
bool IsSystemAdim
);
