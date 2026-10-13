namespace Banking.Api.Contracts;

public sealed record CustomerResponse(CustomerDto Customer);

public sealed record CustomerAccountResponse(string CustomerNumber, IReadOnlyList<AccountDto> Accounts);

public sealed record CustomerRegisterResponse(CustomerDto Customer, string Pin, string SessionToken)
{
    public override string ToString() => "CustomerRegisterResponse(credentials=redacted)";
}

public sealed record CustomerLoginResponse(CustomerDto Customer, string SessionToken)
{
    public override string ToString() => "CustomerLoginResponse(credentials=redacted)";
}
