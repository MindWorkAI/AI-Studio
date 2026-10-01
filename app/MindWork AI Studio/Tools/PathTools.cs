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
    /// The comparer matching <see cref="COMPARISON"/>, for path-keyed lookups.
    /// </summary>
    public static readonly StringComparer COMPARER = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}