using MRPrintHub.Security;
using Xunit;

namespace MRPrintHub.UnitTests.Security;

public class FilenameSanitizerTests
{
    [Theory]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("foo/../../bar.txt", "bar.txt")]
    [InlineData("C:\\Windows\\System32\\evil.exe", "evil.exe")]
    [InlineData("\\\\server\\share\\file.txt", "file.txt")]
    [InlineData("/etc/shadow", "shadow")]
    [InlineData("C:/boot.sys", "boot.sys")]
    public void Sanitize_NeutralizesPathComponents(string raw, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData("a\0b.txt", "ab.txt")]
    [InlineData("a\tb.txt", "ab.txt")]
    [InlineData("a\nb.txt", "ab.txt")]
    public void Sanitize_RemovesNullBytesAndControlChars(string raw, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("COM9")]
    [InlineData("LPT1")]
    [InlineData("LPT9")]
    [InlineData("CON.txt")]
    [InlineData("com3.exe")]
    [InlineData("lpt1.log")]
    public void Sanitize_RejectsReservedDeviceNames(string raw)
    {
        Assert.Equal(string.Empty, FilenameSanitizer.Sanitize(raw));
    }

    [Fact]
    public void Sanitize_DoesNotRejectPlainLookalikeWords()
    {
        Assert.Equal("constitution.txt", FilenameSanitizer.Sanitize("constitution.txt"));
        Assert.Equal("console.log", FilenameSanitizer.Sanitize("console.log"));
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("/", "")]
    [InlineData("\\", "")]
    [InlineData("///", "")]
    [InlineData("..", "")]
    [InlineData(".", "")]
    public void Sanitize_ReturnsEmptyForGarbage(string raw, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData("file name.txt", "file name.txt")]
    [InlineData("café_견본.pdf", "café_견본.pdf")]
    [InlineData("dash-file_1.png", "dash-file_1.png")]
    public void Sanitize_PreservesLegitimateNames(string raw, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData("file.", "file")]
    [InlineData("file .txt. ", "file .txt")]
    public void Sanitize_TrimsTrailingDotsAndSpaces(string raw, string expected)
    {
        Assert.Equal(expected, FilenameSanitizer.Sanitize(raw));
    }

    [Theory]
    [InlineData("a<b.txt")]
    [InlineData("a>b.txt")]
    [InlineData("a:b.txt")]
    [InlineData("a|b.txt")]
    [InlineData("a?b.txt")]
    [InlineData("a*b.txt")]
    public void Sanitize_RemovesWindowsInvalidChars(string raw)
    {
        var result = FilenameSanitizer.Sanitize(raw);
        Assert.DoesNotContain('<', result);
        Assert.DoesNotContain('>', result);
        Assert.DoesNotContain(':', result);
        Assert.DoesNotContain('|', result);
        Assert.DoesNotContain('?', result);
        Assert.DoesNotContain('*', result);
    }

    [Fact]
    public void Sanitize_TruncatesOversizedFilenames()
    {
        var raw = new string('a', 400) + ".txt";
        var result = FilenameSanitizer.Sanitize(raw);

        Assert.True(result.Length <= FilenameSanitizer.MaxFilenameLength);
        Assert.Equal(FilenameSanitizer.MaxFilenameLength, result.Length);
    }
}