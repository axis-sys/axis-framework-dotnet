namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.GetValidationStatus;

/// <summary>Current state of an email-validation run.</summary>
public sealed record GetValidationStatusResponse : IAxisQueryResponse
{
    /// <summary>Saga id of the validation run.</summary>
    public required string SagaId { get; init; }

    /// <summary>
    /// <c>Pending</c>, <c>Running</c> and <c>Compensating</c> are in flight; <c>Completed</c>,
    /// <c>Compensated</c> and <c>Failed</c> are terminal.
    /// </summary>
    /// <example>Completed</example>
    public required string Status { get; init; }

    /// <summary>Whether the email is validated — true once the run completes.</summary>
    public required bool EmailValidated { get; init; }

    /// <summary>Error code of the failure that stopped the run, when it did not complete.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable description of the failure that stopped the run.</summary>
    public string? ErrorMessage { get; init; }
}
