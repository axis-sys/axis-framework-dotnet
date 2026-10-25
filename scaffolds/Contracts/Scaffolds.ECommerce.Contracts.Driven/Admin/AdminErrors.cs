namespace Scaffolds.ECommerce.Contracts.Driven.Admin;

public static class AdminErrors
{
    public static readonly AxisError DatabaseConnectionStringMissing = AxisError.ServiceUnavailable("DATABASE_CONNECTION_STRING_MISSING");
    public static readonly AxisError DatabaseMigrationFailed = AxisError.InternalServerError("DATABASE_MIGRATION_FAILED");
}
