namespace AIStudio.Tools;

/// <summary>
/// Shared rules for comparing file system paths.
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
}