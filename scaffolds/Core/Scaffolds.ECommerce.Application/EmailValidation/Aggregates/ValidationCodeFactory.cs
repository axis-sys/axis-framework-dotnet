using System.Security.Cryptography;
using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;

namespace Scaffolds.ECommerce.Application.EmailValidation.Aggregates;

internal interface IValidationCodeFactory
{
    sealed record NewArgs
    {
        public required CustomerId CustomerId { get; init; }
    }

    Task<AxisResult<IValidationCodeEntityProperties>> CreateAsync(NewArgs args);
}

internal sealed class ValidationCodeFactory(IValidationCodesWritePort validationCodes) : IValidationCodeFactory
{
    public Task<AxisResult<IValidationCodeEntityProperties>> CreateAsync(IValidationCodeFactory.NewArgs args)
        => CreateOrReplaceAsync(new ValidationCodeProperties(args.CustomerId, NewCode()));

    private Task<AxisResult<IValidationCodeEntityProperties>> CreateOrReplaceAsync(IValidationCodeEntityProperties validationCode)
        => validationCodes
            .CreateAsync(validationCode)
            .RecoverConflictAsync(() => validationCodes.UpdateAsync(validationCode));

    private static string NewCode() => RandomNumberGenerator.GetHexString(8, lowercase: true);
}
