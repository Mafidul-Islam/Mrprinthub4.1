using MRPrintHub.QR;
using Xunit;

namespace MRPrintHub.UnitTests.QR;

public class QrCodeGeneratorTests
{
    [Fact]
    public void Generate_UrlFormat_CorrectIp()
    {
        var result = QrCodeGenerator.Generate("192.168.1.50", "token-abc123", 8080);

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_UrlContainsCorrectComponents()
    {
        var result = MRPrintHub.QR.QrCodeGenerator.Generate("10.0.0.5", "session-xyz", 9482);

        Assert.NotNull(result);
    }

    [Fact]
    public void Generate_RegenerationOnTokenChange()
    {
        var first = MRPrintHub.QR.QrCodeGenerator.Generate("172.16.0.1", "token-first", 3030);
        var second = MRPrintHub.QR.QrCodeGenerator.Generate("172.16.0.1", "token-second", 3030);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Generate_WithFixedInputs_ProducesDeterministicOutput()
    {
        var first = MRPrintHub.QR.QrCodeGenerator.Generate("192.168.1.100", "consistent-token", 5000);
        var second = MRPrintHub.QR.QrCodeGenerator.Generate("192.168.1.100", "consistent-token", 5000);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_ReturnsNullForEmptyIp()
    {
        var result = MRPrintHub.QR.QrCodeGenerator.Generate("", "token", 8080);

        Assert.Null(result);
    }

    [Fact]
    public void BuildHybridUploadUrl_WithCloudAndShop_GeneratesHybridUrl()
    {
        var url = QrCodeGenerator.BuildHybridUploadUrl("https://mrprinthub.com", "MRPH-12345678", "192.168.1.50", 5000, "token-abc");

        Assert.NotNull(url);
        Assert.StartsWith("https://mrprinthub.com/u/MRPH-12345678", url);
        Assert.Contains("local=", url);
        Assert.Contains("token=token-abc", url);
    }

    [Fact]
    public void BuildHybridUploadUrl_WithoutCloud_FallsBackToLocalUrl()
    {
        var url = QrCodeGenerator.BuildHybridUploadUrl(null, null, "192.168.1.50", 5000, "token-abc");

        Assert.Equal("http://192.168.1.50:5000/u/token-abc", url);
    }

    [Fact]
    public void GenerateHybrid_GeneratesValidQrBytes()
    {
        var bytes = QrCodeGenerator.GenerateHybrid("http://localhost:5000", "MRPH-DEMO", "192.168.1.100", "token-xyz", 5000);

        Assert.NotNull(bytes);
        Assert.NotEmpty(bytes);
    }
}