using Banking.Domain.Enums;
using Banking.Domain.Models;

namespace Banking.Api.Contracts;

public sealed record AccountDto(
    string AccountNumber, string CustomerNumber, decimal Balance, bool Closed, AccountType AccountType)
{
    public static AccountDto FromDomain(Account account) =>
        new(account.AccountNumber, account.CustomerNumber, account.Balance, account.Closed, account.AccountType);
}
