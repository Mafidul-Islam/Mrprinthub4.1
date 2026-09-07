using MRPrintHub.Core.DTOs;
using MRPrintHub.Core.Enums;

namespace MRPrintHub.Security;

/// <summary>
/// Pure session lifecycle validation: expiry, revocation, and IP binding.
/// Recomputes state from the session's stored fields rather than trusting
/// a potentially stale stored flag.
/// </summary>
public static class SessionValidator
{
    /// <summary>True if the session has passed its expiry instant.</summary>
    public static bool IsExpired(SessionInfo session, DateTime utcNow)
        => session.ExpiresAt <= utcNow;

    /// <summary>True if the session was explicitly revoked.</summary>
    public static bool IsRevoked(SessionInfo session)
        => session.State == SessionState.Revoked;

    /// <summary>
    /// True if the session was issued for <paramref name="currentIp"/> and a
    /// real IP is present (a null/blank IP can never match - defends against
    /// spoofed header scenarios downstream).
    /// </summary>
    public static bool IsIpMatched(SessionInfo session, string? currentIp)
    {
        if (string.IsNullOrEmpty(currentIp))
            return false;

        return string.Equals(session.IpAddress, currentIp, StringComparison.Ordinal);
    }

    /// <summary>
    /// Evaluates the current authoritative session state.
    /// Order matters: revoked beats everything, then expiry, then IP binding.
    /// </summary>
    public static SessionState Evaluate(SessionInfo session, DateTime utcNow, string? currentIp)
    {
        if (IsRevoked(session))
            return SessionState.Revoked;

        if (IsExpired(session, utcNow))
            return SessionState.Expired;

        if (!IsIpMatched(session, currentIp))
            return SessionState.Invalid;

        return SessionState.Active;
    }

    /// <summary>Convenience: true only for a fully valid session right now.</summary>
    public static bool IsActive(SessionInfo session, DateTime utcNow, string? currentIp)
        => Evaluate(session, utcNow, currentIp) == SessionState.Active;

    /// <summary>True if the token prefix matches the logged short prefix.</summary>
    public static bool IsTokenPrefixMatch(string token, string storedPrefix)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(storedPrefix))
            return false;

        return storedPrefix.Length <= token.Length
            && string.Equals(token[..storedPrefix.Length], storedPrefix, StringComparison.Ordinal);
    }
}