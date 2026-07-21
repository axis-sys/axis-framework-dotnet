namespace Scaffolds.ECommerce.Contracts.Driving.Customers.v1.MarkEmailValidated;

/// <summary>Confirmation of the email-validated mark.</summary>
public sealed record MarkEmailValidatedResponse : IAxisCommandResponse
{
    /// <summary>Id of the customer.</summary>
    public required string CustomerId { get; init; }

    /// <summary>Always true after the mark lands.</summary>
    public required bool EmailValidated { get; init; }

    /// <summary>True when the email was already validated before this call (the mark was a no-op).</summary>
    public required bool AlreadyValidated { get; init; }
}
