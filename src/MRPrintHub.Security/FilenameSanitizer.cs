namespace MRPrintHub.Security;

/// <summary>
/// Converts any client-supplied string into a safe, bare, storage-ready filename.
/// Neutralizes path components, NUL/control and Windows-invalid characters,
/// reserved device names, trailing dots/spaces, and enforces the 255-char limit.
/// Pure and fully unit-testable.
/// </summary>
public static class FilenameSanitizer
{
    public const int MaxFilenameLength = 255;

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Returns a safe bare filename, or an empty string when the input yields
    /// nothing usable (empty/garbage input, reserved device name, traversal-only).
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        // Neutralize any path structure: only the last segment survives.
        var lastSegment = GetLastSegment(raw);

        var cleaned = new char[lastSegment.Length];
        var count = 0;

        foreach (var c in lastSegment)
        {
            if (c == '\0' || char.IsControl(c))
                continue;
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0)
                continue;

            cleaned[count++] = c;
        }

        var filename = new string(cleaned, 0, count);

        // Windows treats trailing dots/spaces as part of the directory name - strip them.
        filename = filename.TrimEnd('.', ' ');

        // Never return a "." or ".." result (would traverse when combined with a base dir).
        if (filename is "." or "..")
            return string.Empty;

        if (filename.Length == 0)
            return string.Empty;

        // Reject reserved device names (COM1.., LPT1.., CON, PRN, AUX, NUL).
        var baseName = filename;
        var dot = filename.IndexOf('.');
        if (dot >= 0)
            baseName = filename[..dot];

        if (ReservedDeviceNames.Contains(baseName))
            return string.Empty;

        if (filename.Length > MaxFilenameLength)
            filename = filename[..MaxFilenameLength];

        return filename;
    }

    private static string GetLastSegment(string raw)
    {
        var segments = raw.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 0 ? raw : segments[^1];
    }
}