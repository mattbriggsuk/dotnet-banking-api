using Banking.Domain.Enums;

namespace Banking.Domain.Models;

// Immutable snapshots prevent readers or failed writes from mutating stored balances.
public sealed record Account(
    string AccountNumber,
    string CustomerNumber,
    decimal Balance = 0.00m,
    bool Closed = false,
    AccountType AccountType = AccountType.Current);
