namespace MRPrintHub.Security;

/// <summary>
/// Validates client-supplied filenames against the configured extension
/// allow-list. Case-insensitive; accepts list entries with or without a dot.
/// Doesn't trust the HTTP Content-Type header - only the actual filename.
/// </summary>
public static class ExtensionValidator
{
    /// <summary>
    /// True when the file has an extension that appears in the allow-list.
    /// Files without an extension are rejected.
    /// </summary>
    /// <param name="filename">The (sanitized) client-supplied filename.</param>
    /// <param name="allowedExtensions">e.g. "jpg", ".jpg", "JPG" - normalized internally.</param>
    public static bool IsAllowed(string filename, IEnumerable<string> allowedExtensions)
    {
        if (string.IsNullOrEmpty(filename))
            return false;

        var extension = Path.GetExtension(filename);
        if (string.IsNullOrEmpty(extension))
            return false;

        var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();

        foreach (var allowed in allowedExtensions)
        {
            if (string.IsNullOrWhiteSpace(allowed))
                continue;

            var normalizedAllowed = allowed.Trim().TrimStart('.').ToLowerInvariant();
            if (normalizedAllowed.Length > 0 && normalizedAllowed == normalizedExtension)
                return true;
        }

        return false;
    }
}