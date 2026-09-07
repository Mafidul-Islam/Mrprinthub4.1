namespace MRPrintHub.Security;

/// <summary>
/// Detects attempts to escape the configured storage directory. Pure, I/O-free.
/// </summary>
public static class PathGuard
{
    /// <summary>
    /// True when the value contains a path-traversal payload: a leading/trailing
    /// ".." segment, an absolute path, or a UNC path.
    /// </summary>
    public static bool IsPathTraversal(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        if (IsAbsolutePath(raw) || IsUncPath(raw))
            return true;

        // Normalize separators so "../" and "..\" are treated identically.
        var normalized = raw.Replace('\\', '/');
        var segments = normalized.Split('/');

        foreach (var segment in segments)
        {
            if (segment is ".." or ".")
                return true;
        }

        // A lone ".." (no separator present) is still a traversal attempt.
        return normalized == "..";
    }

    /// <summary>True for rooted paths such as C:\..., C:/..., or /....</summary>
    public static bool IsAbsolutePath(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        return Path.IsPathRooted(raw) || (raw.Length >= 2 && raw[1] == ':');
    }

    /// <summary>True for UNC paths like \\server\share or //server/share.</summary>
    public static bool IsUncPath(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        return raw.StartsWith(@"\\", StringComparison.Ordinal)
            || raw.StartsWith("//", StringComparison.Ordinal);
    }

    /// <summary>True when the value contains a NUL byte.</summary>
    public static bool ContainsNullBytes(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        return raw.IndexOf('\0') >= 0;
    }

    /// <summary>True when the value contains any control character (including NUL).</summary>
    public static bool ContainsControlChars(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return false;

        return raw.Any(char.IsControl);
    }

    /// <summary>
    /// High-level safety check used before accepting any client-supplied filename:
    /// non-empty, no NUL bytes, no path-traversal payload.
    /// </summary>
    public static bool IsSafeFilename(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        if (ContainsNullBytes(raw))
            return false;

        return !IsPathTraversal(raw);
    }
}