using CustomerId = Scaffolds.ECommerce.SharedKernel.ContextIds.CustomerId;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

internal sealed class CustomersRepository(IAxisDbRepository db) : ICustomersPort
{
    private const string Select = $"SELECT {CustomersColumns.All}";

    public Task<AxisResult<ICustomerEntityProperties>> GetByIdAsync(CustomerId customerId)
        => db.GetAsync<ICustomerEntityProperties>(
            $"{Select} FROM {CustomersTable.Table} WHERE {CustomersColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()),
            CustomerDbEntity.FromReader,
            "CUSTOMER_NOT_FOUND");

    public Task<AxisResult<ICustomerEntityProperties>> GetByEmailAsync(string email)
        => db.GetAsync<ICustomerEntityProperties>(
            $"{Select} FROM {CustomersTable.Table} WHERE {CustomersColumns.Email} = @email",
            b => b.Add("email", email),
            CustomerDbEntity.FromReader,
            "CUSTOMER_NOT_FOUND");

    public Task<AxisResult> CreateAsync(ICustomerEntityProperties properties)
        => db.ExecuteAsync(
            $"INSERT INTO {CustomersTable.Table} ({CustomersColumns.All}) VALUES (@customerId, @email, @name, @isAdmin, @externalId, @provider, @emailValidated)",
            b => b.Add("customerId", properties.CustomerId.ToString())
                .Add("email", properties.Email)
                .Add("name", properties.Name)
                .Add("isAdmin", properties.IsAdmin)
                .Add("externalId", properties.ExternalId)
                .Add("provider", properties.Provider)
                .Add("emailValidated", properties.EmailValidated),
            duplicateKeyCode: "CUSTOMER_EMAIL_ALREADY_EXISTS");

    public Task<AxisResult> SetEmailValidatedAsync(CustomerId customerId, bool emailValidated)
        => db.ExecuteAsync(
            $"UPDATE {CustomersTable.Table} SET {CustomersColumns.EmailValidated} = @emailValidated WHERE {CustomersColumns.CustomerId} = @customerId",
            b => b.Add("emailValidated", emailValidated).Add("customerId", customerId.ToString()));
}
