using System;
using System.Security.Cryptography;

namespace MRPrintHub.Cloud.Services;

public interface IShopCodeGenerator
{
    string GenerateShopCode();
    string GenerateDeviceId();
    string GenerateDeviceToken();
}

public class ShopCodeGenerator : IShopCodeGenerator
{
    private const string AlphanumericChars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ"; // Exclude ambiguous 0/O, 1/I

    public string GenerateShopCode()
    {
        // Format: MRPH-XXXXXXXX (8 random secure uppercase alphanumeric chars)
        Span<byte> randomBytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(randomBytes);

        var chars = new char[8];
        for (int i = 0; i < 8; i++)
        {
            chars[i] = AlphanumericChars[randomBytes[i] % AlphanumericChars.Length];
        }

        return $"MRPH-{new string(chars)}";
    }

    public string GenerateDeviceId()
    {
        Span<byte> randomBytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(randomBytes);

        var chars = new char[6];
        for (int i = 0; i < 6; i++)
        {
            chars[i] = AlphanumericChars[randomBytes[i] % AlphanumericChars.Length];
        }

        return $"DEV-{new string(chars)}";
    }

    public string GenerateDeviceToken()
    {
        // 256-bit CSPRNG token, URL-safe Base64
        byte[] bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
