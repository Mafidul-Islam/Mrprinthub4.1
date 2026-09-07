using MRPrintHub.Security;
using Xunit;

namespace MRPrintHub.UnitTests.Security;

public class ExtensionValidatorTests
{
    private static readonly string[] Allowed = ["jpg", ".png", "PDF", " .docx"];

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("photo.JPG")]
    [InlineData("photo2.png")]
    [InlineData("report.pdf")]
    [InlineData("letter.DOCX")]
    public void IsAllowed_AcceptsListedExtensionsCaseInsensitively(string filename)
    {
        Assert.True(ExtensionValidator.IsAllowed(filename, Allowed));
    }

    [Theory]
    [InlineData("photo.gif")]
    [InlineData("photo.txt")]
    [InlineData("photo.exe")]
    [InlineData("photo")]
    [InlineData("photo.")]
    [InlineData("")]
    public void IsAllowed_RejectsUnlistedOrExtensionless(string filename)
    {
        Assert.False(ExtensionValidator.IsAllowed(filename, Allowed));
    }

    [Fact]
    public void IsAllowed_RejectsEmptyFilename()
    {
        Assert.False(ExtensionValidator.IsAllowed("", Allowed));
    }

    [Fact]
    public void IsAllowed_HandlesNullElemsInListGracefully()
    {
        string[] listWithNull = ["jpg", null!, "png"];
        Assert.True(ExtensionValidator.IsAllowed("a.PNG", listWithNull));
    }

    [Fact]
    public void IsAllowed_DoesNotTrustContentType_OnlyExtension()
    {
        // Even with a valid extension, a double-extension like .jpg.exe is rejected.
        Assert.False(ExtensionValidator.IsAllowed("scan.jpg.exe", Allowed));
        Assert.True(ExtensionValidator.IsAllowed("scan.exe.jpg", Allowed));
    }
}