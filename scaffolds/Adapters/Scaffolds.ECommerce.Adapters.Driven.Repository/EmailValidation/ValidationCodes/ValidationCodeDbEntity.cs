using System.Data.Common;
using Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation.ValidationCodes;

internal sealed record ValidationCodeDbEntity(
    CustomerId CustomerId,
    string Code
) : IValidationCodeEntityProperties
{
    internal static ValidationCodeDbEntity FromReader(DbDataReader reader)
        => new(
            (CustomerId)reader.GetString(0),
            reader.GetString(1)
        );
}
