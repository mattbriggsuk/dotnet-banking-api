using Banking.Domain.Enums;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;

namespace Banking.Utils;

public static class TransactionUtils
{
    public static void ValidateTransaction(Transaction? transaction, TransactionType expectedType)
    {
        if (transaction is null)
            throw new ArgumentException("Transaction is required.");
        if (transaction.TransactionType != expectedType)
            throw new ArgumentException($"Transaction type must be {expectedType.ToString().ToUpperInvariant()}.");

        var scale = (decimal.GetBits(transaction.Amount)[3] >> 16) & 0xff;
        if (transaction.Amount <= 0 || transaction.Amount >= 10_000_000_000_000_000m || scale > 2)
            throw new ArgumentException("Amount must be positive with at most two decimal places and 16 integer digits.");
        if (!ValidationUtils.IsValidAccountNumber(transaction.TargetAccountNumber))
            throw new ArgumentException("Target account number must be valid.");

        if (expectedType == TransactionType.Transfer)
        {
            if (!ValidationUtils.IsValidAccountNumber(transaction.SourceAccountNumber))
                throw new ArgumentException("Source account number must be valid for transfers.");
            if (transaction.SourceAccountNumber == transaction.TargetAccountNumber)
                throw new ArgumentException("Source and target accounts must differ.");
        }
        else if (!string.IsNullOrWhiteSpace(transaction.SourceAccountNumber))
        {
            throw new ArgumentException("Source account number is only valid for transfers.");
        }
    }

    public static void RequireFunds(Account account, decimal amount)
    {
        if (account.Balance < amount)
            throw new ConflictException("Insufficient funds.");
    }
}
