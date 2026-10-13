using Banking.Domain.Enums;

namespace Banking.Domain.Models;

public sealed class Transaction
{
    public TransactionType TransactionType { get; init; }
    public decimal Amount { get; init; }
    public string? SourceAccountNumber { get; init; }
    public string TargetAccountNumber { get; init; } = string.Empty;
    public DateTimeOffset? TransactionDate { get; set; }
}
