using Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation.ValidationCodes;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation;

public static class EmailValidationDbInit
{
    public const string Schema = "ECOMMERCE_EMAIL_VALIDATION";

    private static AxisTable[] Tables =>
    [
        ValidationCodesTable.Define(),
    ];

    public static (string Version, string Script)[] Migrations(IAxisSqlDialect sqlDialect) =>
    [
        ("V1", string.Join("\n", Tables.Select(table => table.Render(sqlDialect)))),
    ];
}
