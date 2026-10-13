using System.ComponentModel.DataAnnotations;
using Banking.Domain.Enums;
using Banking.Domain.Models;

namespace Banking.Api.Contracts;

public sealed class AccountTransactionRequest
{
    [Required]
    public TransactionRequest Transaction { get; init; } = null!;
}

public sealed class TransactionRequest
{
    [Required, EnumDataType(typeof(TransactionType))]
    public TransactionType? TransactionType { get; init; }

    [Required]
    public decimal? Amount { get; init; }

    [RegularExpression("[0-9]{16}")]
    public string? SourceAccountNumber { get; init; }

    [Required, RegularExpression("[0-9]{16}")]
    public string TargetAccountNumber { get; init; } = null!;

    public DateTimeOffset? TransactionDate { get; init; }

    public Transaction ToDomain() => new()
    {
        TransactionType = TransactionType!.Value,
        Amount = Amount!.Value,
        SourceAccountNumber = SourceAccountNumber,
        TargetAccountNumber = TargetAccountNumber
    };
}
