namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

public sealed record MarkEmailValidatedCommand : IAxisCommand<MarkEmailValidatedResponse>
{
    public string? CustomerId { get; init; }
}
