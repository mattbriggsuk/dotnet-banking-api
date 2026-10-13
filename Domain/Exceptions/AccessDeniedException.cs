namespace Banking.Domain.Exceptions;

public sealed class AccessDeniedException() : Exception("You cannot access another customer's data.");
