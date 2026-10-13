namespace Banking.Api.Contracts;

public sealed record AccountResponse(AccountDto Account);

public sealed record AccountTransferResponse(AccountDto FromAccount, AccountDto ToAccount);
