namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.RevertEmailValidated;

public sealed record RevertEmailValidatedCommand : IAxisCommand<RevertEmailValidatedResponse>
{
    public string? CustomerId { get; init; }
}
