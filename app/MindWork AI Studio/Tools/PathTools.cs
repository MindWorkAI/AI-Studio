namespace AIStudio.Tools;

/// <summary>
/// Shared rules for comparing and resolving file system paths.
/// </summary>
public static class PathTools
{
    /// <summary>
    /// How to compare file system paths: Windows treats them case-insensitively, Linux and
    /// macOS do not. There, two files may differ in casing alone.
    /// </summary>
    public static readonly StringComparison COMPARISON = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// The comparer matching COMPARISON, for path-keyed lookups.
    /// </summary>
    public static readonly StringComparer COMPARER = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Determines whether a path lies below the given directory.
    /// </summary>
    /// <remarks>
    /// The directory itself does not count as lying below itself. A path which cannot be resolved
    /// counts as lying elsewhere.
    /// </remarks>
    /// <param name="directory">The directory.</param>
    /// <param name="path">The path to check.</param>
    /// <returns>Whether the path lies below the directory.</returns>
    public static bool IsInsideDirectory(string directory, string path)
    {
        try
        {
            var fullDirectory = Path.GetFullPath(directory);
            var directoryPrefix = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? fullDirectory
                : fullDirectory + Path.DirectorySeparatorChar;

            return Path.GetFullPath(path).StartsWith(directoryPrefix, COMPARISON);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the place where a file dialog opens for a path which may no longer exist.
    /// </summary>
    /// <remarks>
    /// Given a file or a folder which was moved or renamed, the native dialog cannot open its place
    /// and falls back to wherever it was used last. The nearest folder which still exists is the
    /// better start: the file or the folder was moved from there, so its new place is often close by.
    /// A relative path, or a path of another operating system, e.g. from a backup, has no such folder.
    /// </remarks>
    /// <param name="path">The file or the folder the dialog should open at.</param>
    /// <returns>The path itself when it exists, else its nearest parent folder which exists, or null when there is none.</returns>
    public static string? FindNearestExistingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            return null;

        if (File.Exists(path) || Directory.Exists(path))
            return path;

        for (var directory = Path.GetDirectoryName(path); !string.IsNullOrEmpty(directory); directory = Path.GetDirectoryName(directory))
            if (Directory.Exists(directory))
                return directory;

        return null;
    }
}