using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Domain.Repositories;
using Banking.Domain.Services;
using Banking.Utils;

namespace Banking.Business.Services;

public sealed class AccountService(
    IAccountRepository accountRepository,
    ICustomerRepository customerRepository,
    TimeProvider timeProvider) : IAccountService
{
    // Registered as a singleton: one lock coordinates all balance changes in this process.
    private readonly object sync = new();

    public Account Create(UserSession userSession, AccountType accountType = AccountType.Current)
    {
        if (userSession is null)
            throw new AuthenticationException("Log in to continue.");
        if (!Enum.IsDefined(accountType))
            throw new ArgumentException("Account type must be CURRENT, SAVINGS or CREDIT.");

        lock (sync)
        {
            if (customerRepository.GetCustomer(userSession.CustomerNumber) is null)
                throw new NotFoundException("Customer not found.");

            string number;
            do
            {
                number = AccountUtils.GenerateAccountNumber();
            } while (accountRepository.GetAccount(number) is not null);

            return accountRepository.Save(new Account(number, userSession.CustomerNumber, AccountType: accountType));
        }
    }

    public Account Close(UserSession userSession, string accountNumber)
    {
        lock (sync)
        {
            var account = GetOwnedAccount(userSession, accountNumber);
            if (account.Balance != 0)
                throw new ConflictException("Account balance must be zero before closing.");

            return accountRepository.Save(account with { Closed = true });
        }
    }

    public Account Deposit(UserSession userSession, Transaction transaction)
    {
        TransactionUtils.ValidateTransaction(transaction, TransactionType.Deposit);
        lock (sync)
        {
            var account = GetOwnedAccount(userSession, transaction.TargetAccountNumber);
            var updated = account with { Balance = ChangeBalance(account.Balance, transaction.Amount) };
            var saved = accountRepository.Save(updated);
            transaction.TransactionDate = timeProvider.GetUtcNow();
            return saved;
        }
    }

    public Account Withdraw(UserSession userSession, Transaction transaction)
    {
        TransactionUtils.ValidateTransaction(transaction, TransactionType.Withdrawal);
        lock (sync)
        {
            var account = GetOwnedAccount(userSession, transaction.TargetAccountNumber);
            TransactionUtils.RequireFunds(account, transaction.Amount);
            var saved = accountRepository.Save(account with { Balance = ChangeBalance(account.Balance, -transaction.Amount) });
            transaction.TransactionDate = timeProvider.GetUtcNow();
            return saved;
        }
    }

    public TransferResult Transfer(UserSession userSession, Transaction transaction)
    {
        TransactionUtils.ValidateTransaction(transaction, TransactionType.Transfer);
        lock (sync)
        {
            var sourceAccount = GetOwnedAccount(userSession, transaction.SourceAccountNumber!);
            var targetAccount = GetAccount(transaction.TargetAccountNumber);
            TransactionUtils.RequireFunds(sourceAccount, transaction.Amount);

            var updatedFrom = sourceAccount with { Balance = ChangeBalance(sourceAccount.Balance, -transaction.Amount) };
            var updatedTo = targetAccount with { Balance = ChangeBalance(targetAccount.Balance, transaction.Amount) };
            accountRepository.TransferBalance(updatedFrom, updatedTo);
            transaction.TransactionDate = timeProvider.GetUtcNow();
            return new TransferResult(updatedFrom, updatedTo);
        }
    }

    private Account GetOwnedAccount(UserSession userSession, string accountNumber)
    {
        var account = GetAccount(accountNumber);
        if (userSession is null || userSession.CustomerNumber != account.CustomerNumber)
            throw new AccessDeniedException();
        return account;
    }

    private Account GetAccount(string accountNumber)
    {
        if (!ValidationUtils.IsValidAccountNumber(accountNumber))
            throw new ArgumentException("Account number must be valid.");

        var account = accountRepository.GetAccount(accountNumber) ?? throw new NotFoundException("Account not found.");
        if (account.Closed)
            throw new ConflictException("Account is closed.");
        return account;
    }

    private static decimal ChangeBalance(decimal balance, decimal amount)
    {
        try
        {
            var result = checked(balance + amount);
            // Decimal can round near its precision limit without throwing an overflow.
            if (result - balance != amount)
                throw new ConflictException("The resulting balance is too large.");
            return result;
        }
        catch (OverflowException)
        {
            throw new ConflictException("The resulting balance is too large.");
        }
    }
}
