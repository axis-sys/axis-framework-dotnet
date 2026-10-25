namespace Scaffolds.ECommerce.Adapters.Driven.Repository.EmailValidation.ValidationCodes;

public static class ValidationCodesTable
{
    public const string Table = $"{EmailValidationDbInit.Schema}.VALIDATION_CODES";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(ValidationCodesColumns.CustomerId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(ValidationCodesColumns.Code, AxisDbType.Varchar(50), notNull: true);
}
