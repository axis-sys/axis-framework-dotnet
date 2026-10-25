namespace Scaffolds.ECommerce.Contracts.Driving.EmailValidation.v1.CancelValidation;

/// <summary>State of an email-validation run once its cancel was requested.</summary>
public sealed record CancelValidationResponse : IAxisCommandResponse
{
    /// <summary>Saga id of the run.</summary>
    public required string SagaId { get; init; }

    /// <summary>
    /// <c>Cancelled</c> when the run stopped at once; <c>Cancelling</c> while the stage in progress finishes, the run
    /// then ending <c>Cancelled</c> before the next stage.
    /// </summary>
    /// <example>Cancelling</example>
    public required string Status { get; init; }
}
