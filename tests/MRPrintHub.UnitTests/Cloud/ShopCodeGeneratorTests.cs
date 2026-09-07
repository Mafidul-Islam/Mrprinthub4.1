using System.Linq;
using MRPrintHub.Cloud.Services;
using Xunit;

namespace MRPrintHub.UnitTests.Cloud;

public class ShopCodeGeneratorTests
{
    private readonly IShopCodeGenerator _generator = new ShopCodeGenerator();

    [Fact]
    public void GenerateShopCode_Format_StartsWithMRPH()
    {
        var code = _generator.GenerateShopCode();

        Assert.NotNull(code);
        Assert.StartsWith("MRPH-", code);
        Assert.Equal(13, code.Length); // "MRPH-" (5) + 8 chars = 13
    }

    [Fact]
    public void GenerateShopCode_IsUniqueAcrossMultipleInvocations()
    {
        var codes = Enumerable.Range(0, 100).Select(_ => _generator.GenerateShopCode()).ToArray();

        Assert.Equal(100, codes.Distinct().Count());
    }

    [Fact]
    public void GenerateDeviceId_Format_StartsWithDEV()
    {
        var deviceId = _generator.GenerateDeviceId();

        Assert.NotNull(deviceId);
        Assert.StartsWith("DEV-", deviceId);
        Assert.Equal(10, deviceId.Length); // "DEV-" (4) + 6 chars = 10
    }

    [Fact]
    public void GenerateDeviceToken_ReturnsUrlSafeToken()
    {
        var token = _generator.GenerateDeviceToken();

        Assert.NotNull(token);
        Assert.True(token.Length >= 40);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}
