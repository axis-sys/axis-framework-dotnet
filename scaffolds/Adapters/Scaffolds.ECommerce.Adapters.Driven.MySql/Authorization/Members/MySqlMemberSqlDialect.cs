using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.MySql.Authorization.Members;

internal sealed class MySqlMemberSqlDialect : IMemberSqlDialect
{
    public string PermissionsParameter => "CAST(@permissions AS JSON)";
}
