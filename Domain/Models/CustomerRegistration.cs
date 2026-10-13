namespace Banking.Domain.Models;

public sealed record CustomerRegistration(Customer Customer, string Pin)
{
    public override string ToString() => "CustomerRegistration(credentials=redacted)";
}
