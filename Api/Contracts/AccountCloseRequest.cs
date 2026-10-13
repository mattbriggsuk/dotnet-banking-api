using System.ComponentModel.DataAnnotations;

namespace Banking.Api.Contracts;

public sealed class AccountCloseRequest
{
    [Required, RegularExpression("[0-9]{16}")]
    public string AccountNumber { get; init; } = null!;
}
