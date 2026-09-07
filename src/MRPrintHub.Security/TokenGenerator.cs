using System.Security.Cryptography;
using MRPrintHub.Core;

namespace MRPrintHub.Security;

/// <summary>
/// Generates cryptographically-secure URL-safe tokens from the OS CSPRNG.
/// Never sequential; each call returns a fresh random value.
/// </summary>
public static class TokenGenerator
{
    /// <summary>
    /// Generates a URL-safe base64 token of the requested number of random bytes.
    /// </summary>
    /// <param name="byteLength">Number of random bytes (default 32 = 256 bits).</param>
    public static string Generate(int byteLength = Constants.TokenLengthBytes)
    {
        if (byteLength < 16)
            throw new ArgumentOutOfRangeException(nameof(byteLength), "At least 16 bytes are required for a secure token.");

        var bytes = new byte[byteLength];
        RandomNumberGenerator.Fill(bytes);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Returns true if the token matches the URL-safe base64 alphabet we emit
    /// (revents garbage route tokens from ever matching while still allowing
    /// consistent downstream hashing).
    /// </summary>
    public static bool HasValidFormat(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length < 22)
            return false;

        foreach (var c in token)
        {
            var valid = (c >= 'A' && c <= 'Z')
                     || (c >= 'a' && c <= 'z')
                     || (c >= '0' && c <= '9')
                     || c == '-' || c == '_';
            if (!valid)
                return false;
        }

        return true;
    }
}