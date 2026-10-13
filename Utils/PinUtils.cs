using System.Globalization;
using System.Security.Cryptography;

namespace Banking.Utils;

public static class PinUtils
{
    private const int Iterations = 600_000;

    public static string Generate() =>
        RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public static string Hash(string pin)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(Derive(pin, salt))}";
    }

    public static bool Matches(string? pin, string encoded)
    {
        var parts = encoded.Split(':');
        if (parts.Length != 2)
            return false;

        try
        {
            var salt = Convert.FromBase64String(parts[0]);
            var expected = Convert.FromBase64String(parts[1]);
            if (salt.Length != 16 || expected.Length != 32)
                return false;

            var actual = Derive(pin ?? string.Empty, salt);
            return pin is not null && CryptographicOperations.FixedTimeEquals(expected, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static byte[] Derive(string pin, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, Iterations, HashAlgorithmName.SHA256, 32);
}
