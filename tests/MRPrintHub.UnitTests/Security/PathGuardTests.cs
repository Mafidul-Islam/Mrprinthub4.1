using MRPrintHub.Security;
using Xunit;

namespace MRPrintHub.UnitTests.Security;

public class PathGuardTests
{
    [Theory]
    [InlineData("..")]
    [InlineData("../")]
    [InlineData("../..")]
    [InlineData("../../etc/passwd")]
    [InlineData("foo/../../bar")]
    [InlineData("..\\")]
    [InlineData("..\\..\\")]
    [InlineData("foo/..")]
    [InlineData("./foo")]
    [InlineData("foo/./bar")]
    public void IsPathTraversal_DetectsDotDotPayloads(string payload)
    {
        Assert.True(PathGuard.IsPathTraversal(payload));
    }

    [Theory]
    [InlineData("C:\\Windows\\System32\\evil.exe")]
    [InlineData("C:/Windows/System32/evil.exe")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\dump.exe")]
    public void IsPathTraversal_DetectsAbsolutePaths(string payload)
    {
        Assert.True(PathGuard.IsPathTraversal(payload));
    }

    [Theory]
    [InlineData("\\\\server\\share\\file.txt")]
    [InlineData("//server/share/file.txt")]
    [InlineData("\\\\.\\pipe\\nope")]
    public void IsPathTraversal_DetectsUncPaths(string payload)
    {
        Assert.True(PathGuard.IsPathTraversal(payload));
    }

    [Theory]
    [InlineData("photo.jpg")]
    [InlineData("report.pdf")]
    [InlineData("some file with spaces.png")]
    [InlineData("normal-folder/file.pdf")]
    [InlineData(".dotfile")]
    public void IsPathTraversal_PassesLegitimateNames(string payload)
    {
        Assert.False(PathGuard.IsPathTraversal(payload));
    }

    [Theory]
    [InlineData("a\0b.txt")]
    [InlineData("\0")]
    public void ContainsNullBytes_DetectsNullByte(string payload)
    {
        Assert.True(PathGuard.ContainsNullBytes(payload));
    }

    [Fact]
    public void ContainsNullBytes_PassesNormal()
    {
        Assert.False(PathGuard.ContainsNullBytes("hello.txt"));
    }

    [Theory]
    [InlineData("a\tb.txt")]
    [InlineData("a\nb.txt")]
    [InlineData("a\rb.txt")]
    public void ContainsControlChars_DetectsControlCharacters(string payload)
    {
        Assert.True(PathGuard.ContainsControlChars(payload));
    }

    [Theory]
    [InlineData("C:\\evil.exe")]
    [InlineData("\\\\server\\share")]
    [InlineData("..\\..\\x")]
    public void IsSafeFilename_RejectsTraversal(string payload)
    {
        Assert.False(PathGuard.IsSafeFilename(payload));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b.txt")]
    public void IsSafeFilename_RejectsEmptySpacesAndNullBytes(string payload)
    {
        Assert.False(PathGuard.IsSafeFilename(payload));
    }
}