using System.Security.Cryptography;
using System.Text;

namespace MRPrintHub.Core.Security;

public static class TokenHasher
{
    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GetPrefix(string token, int length = 8)
    {
        return token.Length <= length ? token : token[..length];
    }
}
