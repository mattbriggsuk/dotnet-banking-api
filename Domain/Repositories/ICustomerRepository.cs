using Banking.Domain.Models;

namespace Banking.Domain.Repositories;

public interface ICustomerRepository
{
    Customer Save(Customer customer, string pinHash);
    Customer? GetCustomer(string customerNumber);
    Customer? GetCustomerByEmail(string email);
    string? GetPinHash(string customerNumber);
}
