using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;
using MRPrintHub.Security;
using Xunit;

namespace MRPrintHub.UnitTests.Security;

public class SessionValidatorTests
{
    private static readonly DateTime Now = new(2026, 8, 31, 12, 0, 0, DateTimeKind.Utc);

    private static SessionInfo Session(
        DateTime expiresAt,
        SessionState state = SessionState.Active,
        string ip = "192.168.1.50")
    {
        return new SessionInfo(
            Token: "token123",
            IpAddress: ip,
            CreatedAt: Now.AddMinutes(-10).AddMilliseconds(0),
            ExpiresAt: expiresAt,
            InterfaceName: "Wi-Fi",
            State: state);
    }

    [Fact]
    public void Evaluate_Active_WhenValidAndIpMatches()
    {
        var s = Session(Now.AddMinutes(30), ip: "192.168.1.50");
        Assert.Equal(SessionState.Active, SessionValidator.Evaluate(s, Now, "192.168.1.50"));
        Assert.True(SessionValidator.IsActive(s, Now, "192.168.1.50"));
    }

    [Fact]
    public void Evaluate_Expired_WhenPastExpiry()
    {
        var s = Session(Now.AddMinutes(-1));
        Assert.Equal(SessionState.Expired, SessionValidator.Evaluate(s, Now, "192.168.1.50"));
        Assert.False(SessionValidator.IsActive(s, Now, "192.168.1.50"));
        Assert.True(SessionValidator.IsExpired(s, Now));
    }

    [Fact]
    public void Evaluate_Expired_AtExactBoundaryInstant()
    {
        var s = Session(Now);
        Assert.Equal(SessionState.Expired, SessionValidator.Evaluate(s, Now, "192.168.1.50"));
    }

    [Fact]
    public void Evaluate_Revoked_WhenStateRevoked()
    {
        var s = Session(Now.AddMinutes(30), state: SessionState.Revoked);
        Assert.Equal(SessionState.Revoked, SessionValidator.Evaluate(s, Now, "192.168.1.50"));
        Assert.True(SessionValidator.IsRevoked(s));
    }

    [Fact]
    public void Evaluate_Revoked_TakesPrecedenceOverExpiry()
    {
        var s = Session(Now.AddMinutes(-5), state: SessionState.Revoked);
        Assert.Equal(SessionState.Revoked, SessionValidator.Evaluate(s, Now, "192.168.1.50"));
    }

    [Fact]
    public void Evaluate_Invalid_WhenIpMismatched()
    {
        var s = Session(Now.AddMinutes(30), ip: "192.168.1.50");
        Assert.Equal(SessionState.Invalid, SessionValidator.Evaluate(s, Now, "192.168.1.99"));
        Assert.False(SessionValidator.IsIpMatched(s, "192.168.1.99"));
    }

    [Fact]
    public void Evaluate_Invalid_WhenNoCurrentIpPresent()
    {
        var s = Session(Now.AddMinutes(30), ip: "192.168.1.50");
        Assert.Equal(SessionState.Invalid, SessionValidator.Evaluate(s, Now, null));
        Assert.False(SessionValidator.IsIpMatched(s, null));
        Assert.False(SessionValidator.IsIpMatched(s, ""));
    }

    [Theory]
    [InlineData("tok_abc123", "tok_abc123", true)]
    [InlineData("tok_abc123", "tok_", true)]
    [InlineData("tok_abc123", "TOK_", false)]
    [InlineData("", "tok", false)]
    [InlineData("tok", "tok_longprefix", false)]
    public void IsTokenPrefixMatch_ComparesPrefixCaseSensitively(string token, string prefix, bool expected)
    {
        Assert.Equal(expected, SessionValidator.IsTokenPrefixMatch(token, prefix));
    }
}