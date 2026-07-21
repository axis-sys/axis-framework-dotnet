namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

public static class CustomersTable
{
    public const string Table = $"{EComDbInit.Schema}.CUSTOMERS";

    public static AxisTable Define() => new AxisTable(Table)
        .Column(CustomersColumns.CustomerId, AxisDbType.Varchar(50), primaryKey: true)
        .Column(CustomersColumns.Email, AxisDbType.Varchar(200), notNull: true)
        .Column(CustomersColumns.Name, AxisDbType.Varchar(200), notNull: true)
        .Column(CustomersColumns.IsAdmin, AxisDbType.Bool, notNull: true)
        .Column(CustomersColumns.ExternalId, AxisDbType.Varchar(200))
        .Column(CustomersColumns.Provider, AxisDbType.Varchar(50))
        .Column(CustomersColumns.EmailValidated, AxisDbType.Bool, notNull: true)
        .Unique("UQ_CUSTOMERS_EMAIL", CustomersColumns.Email);
}
