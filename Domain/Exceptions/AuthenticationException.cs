namespace Banking.Domain.Exceptions;

public sealed class AuthenticationException(string message) : Exception(message);
