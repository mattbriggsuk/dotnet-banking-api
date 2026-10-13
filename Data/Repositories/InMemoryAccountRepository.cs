using Banking.Domain.Models;
using Banking.Domain.Repositories;

namespace Banking.Data.Repositories;

public sealed class InMemoryAccountRepository : IAccountRepository
{
    private readonly object sync = new();
    private readonly Dictionary<string, Account> accounts = new(StringComparer.Ordinal);

    public Account? GetAccount(string accountNumber)
    {
        lock (sync)
            return accounts.GetValueOrDefault(accountNumber);
    }

    public IReadOnlyList<Account> GetCustomerAccounts(string customerNumber)
    {
        lock (sync)
            return accounts.Values.Where(account => account.CustomerNumber == customerNumber).ToArray();
    }

    public Account Save(Account account)
    {
        lock (sync)
        {
            accounts[account.AccountNumber] = account;
            return account;
        }
    }

    public void TransferBalance(Account fromAccount, Account toAccount)
    {
        lock (sync)
        {
            accounts[fromAccount.AccountNumber] = fromAccount;
            accounts[toAccount.AccountNumber] = toAccount;
        }
    }
}
