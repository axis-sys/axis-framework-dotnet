using Scaffolds.ECommerce.Adapters.Driven.Repository.Authorization.Members;

namespace Scaffolds.ECommerce.Adapters.Driven.Postgres.Authorization.Members;

internal sealed class PostgresMemberSqlDialect : IMemberSqlDialect
{
    public string PermissionsParameter => "CAST(@permissions AS JSONB)";
}
