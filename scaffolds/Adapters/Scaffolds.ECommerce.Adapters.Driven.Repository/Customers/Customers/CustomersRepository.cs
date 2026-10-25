using Scaffolds.ECommerce.Contracts.Driven.Customers.Customers;
using Scaffolds.ECommerce.Domain.Customers.Customers;
using Scaffolds.ECommerce.SharedKernel.Shared;

namespace Scaffolds.ECommerce.Adapters.Driven.Repository.Customers.Customers;

internal sealed class CustomersRepository(IAxisDbRepository db) : ICustomersReaderPort, ICustomersWritePort
{
    private const string Select = $"SELECT {CustomersColumns.All}";

    public Task<AxisResult<ICustomerEntityProperties>> GetByIdAsync(CustomerId customerId)
        => db.GetAsync<ICustomerEntityProperties>(
            $"{Select} FROM {CustomersTable.Table} WHERE {CustomersColumns.CustomerId} = @customerId",
            b => b.Add("customerId", customerId.ToString()),
            CustomerDbEntity.FromReader,
            CustomerErrors.CustomerNotFound.Code);

    public Task<AxisResult<ICustomerEntityProperties>> GetByEmailAsync(string email)
        => db.GetAsync<ICustomerEntityProperties>(
            $"{Select} FROM {CustomersTable.Table} WHERE {CustomersColumns.Email} = @email",
            b => b.Add("email", email),
            CustomerDbEntity.FromReader,
            CustomerErrors.CustomerNotFound.Code);

    public Task<AxisResult> CreateAsync(ICustomerEntityProperties customer)
        => db.ExecuteAsync(
            $"INSERT INTO {CustomersTable.Table} ({CustomersColumns.All}) VALUES (@customerId, @email, @name, @isAdmin, @externalId, @provider, @emailValidated)",
            b => b.Add("customerId", customer.CustomerId.ToString())
                .Add("email", customer.Email)
                .Add("name", customer.Name)
                .Add("isAdmin", customer.IsAdmin)
                .Add("externalId", customer.ExternalId)
                .Add("provider", customer.Provider)
                .Add("emailValidated", customer.EmailValidated),
            duplicateKeyCode: CustomerErrors.CustomerEmailAlreadyExists.Code);

    public Task<AxisResult> SetEmailValidatedAsync(CustomerId customerId, bool emailValidated)
        => db.ExecuteAsync(
            $"UPDATE {CustomersTable.Table} SET {CustomersColumns.EmailValidated} = @emailValidated WHERE {CustomersColumns.CustomerId} = @customerId",
            b => b.Add("emailValidated", emailValidated).Add("customerId", customerId.ToString()));
}
