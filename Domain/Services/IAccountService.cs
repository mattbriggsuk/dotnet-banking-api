using Banking.Domain.Enums;
using Banking.Domain.Models;

namespace Banking.Domain.Services;

public interface IAccountService
{
    Account Create(UserSession userSession, AccountType accountType = AccountType.Current);
    Account Close(UserSession userSession, string accountNumber);
    Account Deposit(UserSession userSession, Transaction transaction);
    Account Withdraw(UserSession userSession, Transaction transaction);
    TransferResult Transfer(UserSession userSession, Transaction transaction);
}
