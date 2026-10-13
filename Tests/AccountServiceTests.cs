using Banking.Business.Services;
using Banking.Data.Repositories;
using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Domain.Repositories;
using Banking.Utils;

namespace Banking.Tests;

public sealed class AccountServiceTests
{
    private readonly InMemoryCustomerRepository customers = new();
    private readonly InMemoryAccountRepository accounts = new();
    private readonly TestClock clock = new();
    private readonly AccountService service;
    private readonly UserSession alice;
    private readonly UserSession bob;

    public AccountServiceTests()
    {
        service = new AccountService(accounts, customers, clock);
        alice = AddCustomer("alice@example.com");
        bob = AddCustomer("bob@example.com");
    }

    [Theory]
    [InlineData(AccountType.Current)]
    [InlineData(AccountType.Savings)]
    [InlineData(AccountType.Credit)]
    public void CreatePersistsSelectedTypeForAuthenticatedCustomer(AccountType type)
    {
        var account = service.Create(alice, type);
        Assert.True(ValidationUtils.IsValidAccountNumber(account.AccountNumber));
        Assert.Equal(alice.CustomerNumber, account.CustomerNumber);
        Assert.Equal(type, account.AccountType);
        Assert.Equal(0m, account.Balance);
        Assert.False(account.Closed);
        Assert.Equal(account, accounts.GetAccount(account.AccountNumber));
    }

    [Fact]
    public void CreateRejectsMissingCustomerAndUndefinedType()
    {
        var missing = new UserSession(alice.Customer with { CustomerNumber = AccountUtils.GenerateAccountNumber() }, "test");
        Assert.Throws<NotFoundException>(() => service.Create(missing));
        Assert.Throws<ArgumentException>(() => service.Create(alice, (AccountType)99));
        Assert.Empty(accounts.GetCustomerAccounts(alice.CustomerNumber));
    }

    [Fact]
    public void DepositAndWithdrawalUseDecimalAndServerTime()
    {
        var original = service.Create(alice);
        var deposit = Movement(TransactionType.Deposit, original, 10.10m);
        deposit.TransactionDate = clock.Now.AddYears(-10);
        var deposited = service.Deposit(alice, deposit);
        Assert.Equal(10.10m, deposited.Balance);
        Assert.Equal(clock.Now, deposit.TransactionDate);
        Assert.Equal(0m, original.Balance);
        var withdrawal = Movement(TransactionType.Withdrawal, original, 0.10m);
        Assert.Equal(10m, service.Withdraw(alice, withdrawal).Balance);
        Assert.Equal(clock.Now, withdrawal.TransactionDate);
    }

    [Fact]
    public void LifecycleRejectsNonzeroBalanceRepeatedCloseAndClosedAccountMovement()
    {
        var account = service.Create(alice);
        service.Deposit(alice, Movement(TransactionType.Deposit, account, 5m));
        Assert.Throws<ConflictException>(() => service.Close(alice, account.AccountNumber));
        Assert.False(accounts.GetAccount(account.AccountNumber)!.Closed);
        service.Withdraw(alice, Movement(TransactionType.Withdrawal, account, 5m));
        Assert.True(service.Close(alice, account.AccountNumber).Closed);
        Assert.Throws<ConflictException>(() => service.Close(alice, account.AccountNumber));
        Assert.Throws<ConflictException>(() => service.Deposit(alice, Movement(TransactionType.Deposit, account, 1m)));
        Assert.Throws<ConflictException>(() => service.Withdraw(alice, Movement(TransactionType.Withdrawal, account, 1m)));
    }

    [Fact]
    public void OwnershipIsEnforcedByBusinessLayer()
    {
        var account = service.Create(alice);
        service.Deposit(alice, Movement(TransactionType.Deposit, account, 10m));
        Assert.Throws<AccessDeniedException>(() => service.Close(bob, account.AccountNumber));
        Assert.Throws<AccessDeniedException>(() => service.Deposit(bob, Movement(TransactionType.Deposit, account, 1m)));
        Assert.Throws<AccessDeniedException>(() => service.Withdraw(bob, Movement(TransactionType.Withdrawal, account, 1m)));
        var target = service.Create(bob);
        Assert.Throws<AccessDeniedException>(() => service.Transfer(bob,
            Movement(TransactionType.Transfer, target, 1m, account.AccountNumber)));
        Assert.Equal(10m, accounts.GetAccount(account.AccountNumber)!.Balance);
    }

    [Fact]
    public void TransferCanCreditAnotherCustomerAndConservesFunds()
    {
        var source = service.Create(alice);
        var target = service.Create(bob);
        service.Deposit(alice, Movement(TransactionType.Deposit, source, 10m));
        var transaction = Movement(TransactionType.Transfer, target, 3.25m, source.AccountNumber);
        var result = service.Transfer(alice, transaction);
        Assert.Equal(6.75m, result.FromAccount.Balance);
        Assert.Equal(3.25m, result.ToAccount.Balance);
        Assert.Equal(clock.Now, transaction.TransactionDate);
        Assert.Equal(result.FromAccount, accounts.GetAccount(source.AccountNumber));
        Assert.Equal(result.ToAccount, accounts.GetAccount(target.AccountNumber));
    }

    [Fact]
    public void RejectedMovementsLeaveBothBalancesUnchanged()
    {
        var source = service.Create(alice);
        var target = service.Create(bob);
        service.Deposit(alice, Movement(TransactionType.Deposit, source, 10m));
        Assert.Throws<ConflictException>(() => service.Withdraw(alice, Movement(TransactionType.Withdrawal, source, 11m)));
        Assert.Throws<ConflictException>(() => service.Transfer(alice, Movement(TransactionType.Transfer, target, 11m, source.AccountNumber)));
        Assert.Throws<ArgumentException>(() => service.Transfer(alice, Movement(TransactionType.Transfer, source, 1m, source.AccountNumber)));
        Assert.Throws<NotFoundException>(() => service.Transfer(alice, new Transaction
        {
            TransactionType = TransactionType.Transfer,
            Amount = 1m,
            SourceAccountNumber = source.AccountNumber,
            TargetAccountNumber = AccountUtils.GenerateAccountNumber()
        }));
        service.Close(bob, target.AccountNumber);
        Assert.Throws<ConflictException>(() => service.Transfer(alice, Movement(TransactionType.Transfer, target, 1m, source.AccountNumber)));
        Assert.Equal(10m, accounts.GetAccount(source.AccountNumber)!.Balance);
        Assert.Equal(0m, accounts.GetAccount(target.AccountNumber)!.Balance);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("0.001")]
    [InlineData("1.000")]
    [InlineData("10000000000000000")]
    public void InvalidAmountsAreRejectedBeforeBalanceChanges(string amount)
    {
        var account = service.Create(alice);
        Assert.Throws<ArgumentException>(() => service.Deposit(alice,
            Movement(TransactionType.Deposit, account, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture))));
        Assert.Equal(0m, accounts.GetAccount(account.AccountNumber)!.Balance);
    }

    [Fact]
    public void WrongTypeAndInvalidAccountNumbersAreRejected()
    {
        var account = service.Create(alice);
        Assert.Throws<ArgumentException>(() => service.Deposit(alice, Movement(TransactionType.Withdrawal, account, 1m)));
        Assert.Throws<ArgumentException>(() => service.Deposit(alice, Movement(TransactionType.Deposit, account, 1m, account.AccountNumber)));
        Assert.Throws<ArgumentException>(() => service.Close(alice, "1234567890123456"));
        Assert.Throws<ArgumentException>(() => service.Deposit(alice, null!));
    }

    [Fact]
    public void ConcurrentDepositsAndTransfersDoNotLoseUpdates()
    {
        var source = service.Create(alice);
        var target = service.Create(bob);
        Parallel.For(0, 200, _ => service.Deposit(alice, Movement(TransactionType.Deposit, source, 1m)));
        Parallel.For(0, 100, _ => service.Transfer(alice, Movement(TransactionType.Transfer, target, 1m, source.AccountNumber)));
        Assert.Equal(100m, accounts.GetAccount(source.AccountNumber)!.Balance);
        Assert.Equal(100m, accounts.GetAccount(target.AccountNumber)!.Balance);
    }

    [Fact]
    public void FailedRepositoryWritesCannotMutateStoredSnapshots()
    {
        var source = service.Create(alice);
        var target = service.Create(bob);
        service.Deposit(alice, Movement(TransactionType.Deposit, source, 10m));
        var failing = new AccountService(new RejectingRepository(accounts), customers, clock);
        Assert.Throws<InvalidOperationException>(() => failing.Deposit(alice, Movement(TransactionType.Deposit, source, 1m)));
        Assert.Throws<InvalidOperationException>(() => failing.Close(bob, target.AccountNumber));
        Assert.Throws<InvalidOperationException>(() => failing.Transfer(alice, Movement(TransactionType.Transfer, target, 1m, source.AccountNumber)));
        Assert.Equal(10m, accounts.GetAccount(source.AccountNumber)!.Balance);
        Assert.Equal(0m, accounts.GetAccount(target.AccountNumber)!.Balance);
        Assert.False(accounts.GetAccount(target.AccountNumber)!.Closed);
    }

    [Fact]
    public void DecimalOverflowAndPrecisionLossCannotCreditOrDebitFunds()
    {
        var source = service.Create(alice);
        var target = service.Create(bob);
        accounts.Save(source with { Balance = 10m });
        accounts.Save(target with { Balance = decimal.MaxValue });
        Assert.Throws<ConflictException>(() => service.Deposit(bob, Movement(TransactionType.Deposit, target, 1m)));
        Assert.Throws<ConflictException>(() => service.Deposit(bob, Movement(TransactionType.Deposit, target, 0.01m)));
        Assert.Throws<ConflictException>(() => service.Transfer(alice, Movement(TransactionType.Transfer, target, 0.01m, source.AccountNumber)));
        Assert.Equal(10m, accounts.GetAccount(source.AccountNumber)!.Balance);
        Assert.Equal(decimal.MaxValue, accounts.GetAccount(target.AccountNumber)!.Balance);
        accounts.Save(source with { Balance = decimal.MaxValue });
        accounts.Save(target with { Balance = 0m });
        Assert.Throws<ConflictException>(() => service.Withdraw(alice, Movement(TransactionType.Withdrawal, source, 0.01m)));
        Assert.Throws<ConflictException>(() => service.Transfer(alice, Movement(TransactionType.Transfer, target, 0.01m, source.AccountNumber)));
        Assert.Equal(decimal.MaxValue, accounts.GetAccount(source.AccountNumber)!.Balance);
        Assert.Equal(0m, accounts.GetAccount(target.AccountNumber)!.Balance);
    }

    private UserSession AddCustomer(string email)
    {
        var customer = customers.Save(new Customer(AccountUtils.GenerateAccountNumber(), "Test", "Customer", email), "test-hash");
        return new UserSession(customer, "test-session");
    }

    private static Transaction Movement(TransactionType type, Account target, decimal amount, string? source = null) => new()
    {
        TransactionType = type,
        Amount = amount,
        SourceAccountNumber = source,
        TargetAccountNumber = target.AccountNumber
    };

    private sealed class RejectingRepository(IAccountRepository inner) : IAccountRepository
    {
        public Account? GetAccount(string number) => inner.GetAccount(number);
        public IReadOnlyList<Account> GetCustomerAccounts(string number) => inner.GetCustomerAccounts(number);
        public Account Save(Account account) => throw new InvalidOperationException("Write rejected.");
        public void TransferBalance(Account fromAccount, Account toAccount) => throw new InvalidOperationException("Write rejected.");
    }
}
