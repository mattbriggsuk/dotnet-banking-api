using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Domain.Repositories;
using Banking.Domain.Services;
using Banking.Utils;

namespace Banking.Business.Services;

public sealed class CustomerService(
    ICustomerRepository customerRepository,
    IAccountRepository accountRepository) : ICustomerService
{
    public CustomerRegistration Register(Customer customer)
    {
        if (!ValidationUtils.IsValidCustomerObject(customer))
            throw new ArgumentException("Customer and all required details must be provided.");
        if (customerRepository.GetCustomerByEmail(customer.Email) is not null)
            throw new ConflictException("A customer with this email already exists. Log in instead.");

        string number;
        do
        {
            number = AccountUtils.GenerateAccountNumber();
        } while (customerRepository.GetCustomer(number) is not null);

        var pin = PinUtils.Generate();
        var registered = new Customer(number, customer.FirstName.Trim(), customer.LastName.Trim(),
            customer.Email.Trim().ToLowerInvariant());
        var saved = customerRepository.Save(registered, PinUtils.Hash(pin));
        return new CustomerRegistration(saved, pin);
    }

    public Customer GetCustomer(UserSession userSession, string customerNumber)
    {
        if (!ValidationUtils.IsValidCustomerNumber(customerNumber))
            throw new ArgumentException("Customer number must be a valid 16-digit number.");
        if (userSession is null || userSession.CustomerNumber != customerNumber)
            throw new AccessDeniedException();

        return customerRepository.GetCustomer(customerNumber) ?? throw new NotFoundException("Customer not found.");
    }

    public IReadOnlyList<Account> GetCustomerAccounts(UserSession userSession, string customerNumber)
    {
        GetCustomer(userSession, customerNumber);
        return accountRepository.GetCustomerAccounts(customerNumber);
    }
}
