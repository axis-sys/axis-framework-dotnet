namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Auth.ValidationCodes;

public static class ValidationCodesTable
{
    public const string Table = $"{EComDbInit.Schema}.VALIDATION_CODES";

    // One pending code per customer — CustomerId is the primary key, so a re-request replaces the
    // previous code (ValidationCodesRepository.SaveAsync implements the replace via AxisResult.RecoverConflict).
    public static AxisTable Define() => new AxisTable(Table)
        .Column(ValidationCodesColumns.CustomerId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(ValidationCodesColumns.Code, AxisDbType.Varchar(50), notNull: true);
}
