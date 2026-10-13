using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Domain.Repositories;

namespace Banking.Data.Repositories;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private sealed record StoredCustomer(Customer Customer, string PinHash);

    private readonly object sync = new();
    private readonly Dictionary<string, StoredCustomer> customers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> emails = new(StringComparer.OrdinalIgnoreCase);

    public Customer Save(Customer customer, string pinHash)
    {
        lock (sync)
        {
            if (emails.ContainsKey(customer.Email.Trim()))
                throw new ConflictException("A customer with this email already exists. Log in instead.");
            if (customers.ContainsKey(customer.CustomerNumber))
                throw new ConflictException("Customer number already exists.");

            customers.Add(customer.CustomerNumber, new StoredCustomer(customer, pinHash));
            emails.Add(customer.Email.Trim(), customer.CustomerNumber);
            return customer;
        }
    }

    public Customer? GetCustomer(string customerNumber)
    {
        lock (sync)
            return customers.GetValueOrDefault(customerNumber)?.Customer;
    }

    public Customer? GetCustomerByEmail(string email)
    {
        lock (sync)
            return emails.TryGetValue(email.Trim(), out var number) ? customers[number].Customer : null;
    }

    public string? GetPinHash(string customerNumber)
    {
        lock (sync)
            return customers.GetValueOrDefault(customerNumber)?.PinHash;
    }
}
