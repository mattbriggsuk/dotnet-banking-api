namespace Banking.Domain.Models;

public sealed record Session(string CustomerNumber, DateTimeOffset ExpiresAt);
