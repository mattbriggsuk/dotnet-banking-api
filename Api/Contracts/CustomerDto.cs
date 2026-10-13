using Banking.Domain.Models;

namespace Banking.Api.Contracts;

public sealed record CustomerDto(string CustomerNumber, string FirstName, string LastName, string Email)
{
    public static CustomerDto FromDomain(Customer customer) =>
        new(customer.CustomerNumber, customer.FirstName, customer.LastName, customer.Email);
}
