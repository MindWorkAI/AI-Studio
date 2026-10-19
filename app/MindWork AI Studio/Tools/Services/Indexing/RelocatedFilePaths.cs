namespace AIStudio.Tools.Services.Indexing;

/// <summary>
/// Reads paths stored by the computer which created an index. Its path syntax may differ from
/// that of the computer restoring the backup; never resolve an old absolute path on this machine.
/// </summary>
internal static class RelocatedFilePaths
{
    internal static bool IsWindowsAbsolute(string path) => path.StartsWith(@"\\", StringComparison.Ordinal)
        || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] is '\\' or '/');

    internal static bool IsLocalAbsolute(string path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path)
        && (OperatingSystem.IsWindows() || !IsWindowsAbsolute(path));

    internal static string GetStoredFileName(string path)
    {
        var separator = IsWindowsAbsolute(path) ? Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\')) : path.LastIndexOf('/');
        return path[(separator + 1)..];
    }

    /// <summary>
    /// Maps a stored relative path below a newly selected root, including drive roots and mounted
    /// volumes. Backslashes are separators only for a Windows index: they are legal Unix file names.
    /// </summary>
    internal static string? Resolve(string root, string oldAbsolutePath, string relativePath)
    {
        if (!IsLocalAbsolute(root) || string.IsNullOrWhiteSpace(relativePath))
            return null;

        var windowsSource = IsWindowsAbsolute(oldAbsolutePath);
        if (!windowsSource && !oldAbsolutePath.StartsWith('/'))
            return null;

        var parts = relativePath.Split(windowsSource ? ['/', '\\'] : ['/']);
        foreach (var part in parts)
        {
            // Check after decoding the source separators, so a Windows '..\\' cannot evade the
            // containment check on Unix. Reject target-invalid names rather than changing them.
            if (part is "" or "." or ".." || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            if (windowsSource && part.Contains(':'))
                return null;

            if (OperatingSystem.IsWindows() && (part.EndsWith('.') || part.EndsWith(' ')))
                return null;
        }

        try
        {
            var target = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
            return PathTools.IsInsideDirectory(root, target) ? target : null;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
