using System.Security.Cryptography;
using System.Text;

namespace FOOD_MAP.Services;

public static class PasswordHasher
{
    public static string Hash(string plainText)
    {
        if (string.IsNullOrWhiteSpace(plainText))
        {
            return string.Empty;
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(plainText.Trim());
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
