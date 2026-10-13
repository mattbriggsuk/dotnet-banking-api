namespace Banking.Domain.Models;

public sealed record UserSession(Customer Customer, string SessionToken)
{
    public string CustomerNumber => Customer.CustomerNumber;

    public override string ToString() => "UserSession(credentials=redacted)";
}
