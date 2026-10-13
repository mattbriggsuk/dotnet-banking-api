using System.ComponentModel.DataAnnotations;
using Banking.Domain.Enums;

namespace Banking.Api.Contracts;

public sealed class AccountCreateRequest
{
    [Required, EnumDataType(typeof(AccountType))]
    public AccountType? AccountType { get; init; } = Domain.Enums.AccountType.Current;
}
