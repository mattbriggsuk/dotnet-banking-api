using Banking.Domain.Models;

namespace Banking.Domain.Services;

public interface ICustomerService
{
    CustomerRegistration Register(Customer customer);
    Customer GetCustomer(UserSession userSession, string customerNumber);
    IReadOnlyList<Account> GetCustomerAccounts(UserSession userSession, string customerNumber);
}
