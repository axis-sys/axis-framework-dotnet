using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Auth.ValidationCodes;

internal sealed class ValidationCodesRepository(IAxisDbRepository db) : IValidationCodesPort
{
    public Task<AxisResult> SaveAsync(CustomerId customerId, string code)
        => CreateAsync(customerId, code)
            .RecoverConflictAsync(() => UpdateAsync(customerId, code))
            .ToAxisResultAsync();

    public Task<AxisResult<string>> GetAsync(CustomerId customerId)
        => db.GetAsync<string>(
            $"SELECT {ValidationCodesColumns.Code} FROM {ValidationCodesTable.Table} WHERE {ValidationCodesColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()),
            reader => reader.GetString(0),
            "EMAIL_VALIDATION_CODE_NOT_FOUND");

    public Task<AxisResult> RemoveAsync(CustomerId customerId)
        => db.ExecuteAsync(
            $"DELETE FROM {ValidationCodesTable.Table} WHERE {ValidationCodesColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()));

    private Task<AxisResult<string>> CreateAsync(CustomerId customerId, string code)
        => db.ExecuteAsync(
                $"INSERT INTO {ValidationCodesTable.Table} ({ValidationCodesColumns.All}) VALUES (@customerId, @code)",
                b => b.Add("customerId", customerId.ToString()).Add("code", code),
                duplicateKeyCode: "VALIDATION_CODE_ALREADY_EXISTS")
            .WithValueAsync(code);

    private Task<AxisResult<string>> UpdateAsync(CustomerId customerId, string code)
        => db.ExecuteAsync(
                $"UPDATE {ValidationCodesTable.Table} SET {ValidationCodesColumns.Code} = @code WHERE {ValidationCodesColumns.CustomerId} = @customerId",
                b => b.Add("code", code).Add("customerId", customerId.ToString()))
            .WithValueAsync(code);
}
