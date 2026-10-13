using System.Security.Cryptography;

namespace Banking.Utils;

public static class AccountUtils
{
    public static string GenerateAccountNumber()
    {
        Span<char> digits = stackalloc char[16];
        digits[0] = (char)('0' + RandomNumberGenerator.GetInt32(1, 10));
        var sum = 0;
        for (var i = 0; i < 15; i++)
        {
            if (i > 0)
                digits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));

            var digit = digits[i] - '0';
            if (i % 2 == 0)
                digit = digit * 2 > 9 ? digit * 2 - 9 : digit * 2;
            sum += digit;
        }

        digits[15] = (char)('0' + (10 - sum % 10) % 10);
        return new string(digits);
    }
}
