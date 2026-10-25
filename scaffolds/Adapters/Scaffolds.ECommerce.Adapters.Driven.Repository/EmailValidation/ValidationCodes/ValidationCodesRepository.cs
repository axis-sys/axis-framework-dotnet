using Scaffolds.ECommerce.Contracts.Driven.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.Domain.EmailValidation.ValidationCodes;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation.ValidationCodes;

internal sealed class ValidationCodesRepository(IAxisDbRepository db) : IValidationCodesReaderPort, IValidationCodesWritePort
{
    private const string Select = $"SELECT {ValidationCodesColumns.All}";

    public Task<AxisResult<IValidationCodeEntityProperties>> GetByCustomerIdAsync(CustomerId customerId)
        => db.GetAsync<IValidationCodeEntityProperties>(
            $"{Select} FROM {ValidationCodesTable.Table} WHERE {ValidationCodesColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()),
            ValidationCodeDbEntity.FromReader,
            ValidationCodeErrors.ValidationCodeNotFound.Code);

    public Task<AxisResult<IValidationCodeEntityProperties>> CreateAsync(IValidationCodeEntityProperties validationCode)
        => db.ExecuteAsync(
                $"INSERT INTO {ValidationCodesTable.Table} ({ValidationCodesColumns.All}) VALUES (@customerId, @code)",
                b => b.Add("customerId", validationCode.CustomerId.ToString()).Add("code", validationCode.Code),
                duplicateKeyCode: ValidationCodeErrors.ValidationCodeAlreadyExists.Code)
            .WithValueAsync(validationCode);

    public Task<AxisResult<IValidationCodeEntityProperties>> UpdateAsync(IValidationCodeEntityProperties validationCode)
        => db.ExecuteAsync(
                $"UPDATE {ValidationCodesTable.Table} SET {ValidationCodesColumns.Code} = @code WHERE {ValidationCodesColumns.CustomerId} = @customerId",
                b => b.Add("code", validationCode.Code).Add("customerId", validationCode.CustomerId.ToString()))
            .WithValueAsync(validationCode);

    public Task<AxisResult> RemoveAsync(CustomerId customerId)
        => db.ExecuteAsync(
            $"DELETE FROM {ValidationCodesTable.Table} WHERE {ValidationCodesColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()));
}
