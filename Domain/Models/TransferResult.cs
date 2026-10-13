namespace Banking.Domain.Models;

public sealed record TransferResult(Account FromAccount, Account ToAccount);
