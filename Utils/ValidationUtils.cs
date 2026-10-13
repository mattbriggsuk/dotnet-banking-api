using System.ComponentModel.DataAnnotations;
using Banking.Domain.Models;

namespace Banking.Utils;

public static class ValidationUtils
{
    public static bool IsValidAccountNumber(string? value)
    {
        if (value is null || value.Length != 16)
            return false;

        var sum = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] is < '0' or > '9')
                return false;

            var digit = value[i] - '0';
            if (i % 2 == 0)
                digit = digit * 2 > 9 ? digit * 2 - 9 : digit * 2;
            sum += digit;
        }

        return sum % 10 == 0;
    }

    public static bool IsValidCustomerNumber(string? value) => IsValidAccountNumber(value);

    public static bool IsValidCustomerObject(Customer? customer) =>
        customer is not null
        && !string.IsNullOrWhiteSpace(customer.FirstName)
        && !string.IsNullOrWhiteSpace(customer.LastName)
        && !string.IsNullOrWhiteSpace(customer.Email)
        && new EmailAddressAttribute().IsValid(customer.Email.Trim());
}
