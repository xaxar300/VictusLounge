using System.Security.Cryptography;
using System.Text;

namespace VictusLounge.Helpers;

public static class PasswordHasher
{
    private const string Prefix = "sha256$";
    private const string DemoSalt = "VictusLounge.Coursework.DemoSalt";

    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{DemoSalt}:{password}"));
        return Prefix + Convert.ToBase64String(bytes);
    }

    public static bool VerifyPassword(string password, string storedHash)
    {
        return string.Equals(storedHash, HashPassword(password), StringComparison.Ordinal);
    }

    public static bool IsHashed(string value)
    {
        return value.StartsWith(Prefix, StringComparison.Ordinal);
    }
}
