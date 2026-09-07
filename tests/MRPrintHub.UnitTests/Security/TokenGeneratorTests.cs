using System.Text.RegularExpressions;
using MRPrintHub.Security;
using Xunit;

namespace MRPrintHub.UnitTests.Security;

public class TokenGeneratorTests
{
    [Fact]
    public void Generate_ReturnsUrlSafeBase64()
    {
        var token = TokenGenerator.Generate();

        Assert.Matches(new Regex("^[A-Za-z0-9_-]+$"), token);
        Assert.DoesNotContain('=', token);
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
    }

    [Fact]
    public void Generate_IsUnique()
    {
        var tokens = Enumerable.Range(0, 100).Select(_ => TokenGenerator.Generate()).ToArray();

        Assert.Equal(100, tokens.Distinct().Count());
    }

    [Fact]
    public void Generate_DefaultLength_IsAtLeast32BytesWorth()
    {
        var token = TokenGenerator.Generate();

        // 32 bytes -> 44 base64 chars, minus 1 padding strip
        Assert.True(token.Length >= 43, $"Expected token length >= 43, was {token.Length}");
    }

    [Fact]
    public void Generate_ThrowsWhenTooShort()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TokenGenerator.Generate(4));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("CON")]
    [InlineData("token-with spaces")]
    public void HasValidFormat_RejectsGarbageAndTooShort(string token)
    {
        Assert.False(TokenGenerator.HasValidFormat(token));
    }

    [Fact]
    public void HasValidFormat_AcceptsGeneratedToken()
    {
        Assert.True(TokenGenerator.HasValidFormat(TokenGenerator.Generate()));
    }
}