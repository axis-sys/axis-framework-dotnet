namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

public sealed record MarkEmailValidatedResponse : IAxisCommandResponse
{
    public required string CustomerId { get; init; }

    public required bool EmailValidated { get; init; }

    public required bool AlreadyValidated { get; init; }
}
