namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

public sealed record RevertEmailValidatedResponse : IAxisCommandResponse
{
    public required string CustomerId { get; init; }

    public required bool EmailValidated { get; init; }
}
