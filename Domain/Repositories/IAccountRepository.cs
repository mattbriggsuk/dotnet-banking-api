using Banking.Domain.Models;

namespace Banking.Domain.Repositories;

public interface IAccountRepository
{
    Account? GetAccount(string accountNumber);
    IReadOnlyList<Account> GetCustomerAccounts(string customerNumber);
    Account Save(Account account);

    // Implementations must persist both snapshots atomically or leave both unchanged.
    void TransferBalance(Account fromAccount, Account toAccount);
}
