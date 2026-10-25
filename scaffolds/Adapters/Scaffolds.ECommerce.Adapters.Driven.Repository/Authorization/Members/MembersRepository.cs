using Scaffolds.ECommerce.Contracts.Driven.Authorization.Members;
using Scaffolds.ECommerce.Domain.Authorization.Members;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

internal sealed class MembersRepository(IAxisDbRepository db, IMemberSqlDialect sqlDialect) : IMembersReaderPort, IMembersWritePort
{
    private const string Select = $"SELECT {MembersColumns.All}";

    public Task<AxisResult<IMemberEntityProperties>> GetByCustomerIdAsync(CustomerId customerId)
        => db.GetAsync<IMemberEntityProperties>(
            $"{Select} FROM {MembersTable.Table} WHERE {MembersColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()),
            MemberDbEntity.FromReader,
            MemberErrors.MemberNotFound.Code);

    public Task<AxisResult<IMemberEntityProperties>> CreateAsync(IMemberEntityProperties member)
        => db.ExecuteAsync(
                $"INSERT INTO {MembersTable.Table} ({MembersColumns.All}) VALUES (@customerId, @disabled, {sqlDialect.PermissionsParameter})",
                b => b.Add("customerId", member.CustomerId.ToString())
                    .Add("disabled", member.Disabled)
                    .AddJson("permissions", AxisJsonColumn.WriteStrings(member.Permissions)),
                duplicateKeyCode: MemberErrors.MemberAlreadyExists.Code)
            .WithValueAsync(member);

    public Task<AxisResult<IMemberEntityProperties>> UpdateAsync(IMemberEntityProperties member)
        => db.ExecuteAsync(
                $"UPDATE {MembersTable.Table} SET {MembersColumns.Disabled} = @disabled, {MembersColumns.Permissions} = {sqlDialect.PermissionsParameter} WHERE {MembersColumns.CustomerId} = @customerId",
                b => b.Add("disabled", member.Disabled)
                    .AddJson("permissions", AxisJsonColumn.WriteStrings(member.Permissions))
                    .Add("customerId", member.CustomerId.ToString()))
            .WithValueAsync(member);
}
