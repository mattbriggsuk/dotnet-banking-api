using System.ComponentModel.DataAnnotations;
using Banking.Domain.Models;

namespace Banking.Api.Contracts;

public sealed class CustomerRegisterRequest
{
    [Required]
    public CustomerRegistrationDetails Customer { get; init; } = null!;
}

public sealed class CustomerRegistrationDetails
{
    // Accepted for compatibility; the server always generates the customer number.
    public string? CustomerNumber { get; init; }

    [Required]
    public string FirstName { get; init; } = null!;

    [Required]
    public string LastName { get; init; } = null!;

    [Required, EmailAddress]
    public string Email { get; init; } = null!;

    public Customer ToDomain() => new(string.Empty, FirstName, LastName, Email);
}
