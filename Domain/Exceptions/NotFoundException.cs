namespace Banking.Domain.Exceptions;

public sealed class NotFoundException(string message) : Exception(message);
