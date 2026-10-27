using AIStudio.Tools.Rust;

namespace AIStudio.Tools;

/// <summary>
/// Picks the file which a picker for exactly one file takes from the paths dropped onto it.
/// </summary>
public static class DroppedFile
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(DroppedFile).Namespace, nameof(DroppedFile));

    /// <summary>
    /// Finds the first dropped path which leads to a file of an allowed type.
    /// </summary>
    /// <remarks>
    /// A picker carries exactly one file, so a multi-selection cannot be honored as a whole. A dropped
    /// folder is rejected instead of being read as "the first file inside it": the user was asked for a
    /// file, and picking one for them would be a surprise.
    /// </remarks>
    /// <param name="paths">The dropped paths, in the order the runtime delivered them.</param>
    /// <param name="filter">The allowed file types, or none to allow every file.</param>
    /// <returns>The path of the file to take, or null when no dropped path leads to a usable file.</returns>
    public static string? FirstUsable(IReadOnlyList<string> paths, FileTypeFilter[]? filter) => paths.FirstOrDefault(path => File.Exists(path) && (filter is not { Length: > 0 } || FileTypes.IsAllowedPath(path, filter)));

    /// <summary>
    /// Explains why none of the dropped paths could be used.
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public static string WarningFor(IReadOnlyList<string> paths)
    {
        // Naming the actual mistake beats a generic "that did not work". Dropping a folder onto a
        // file picker is the likeliest of them:
        if (paths.Any(Directory.Exists))
            return TB("Please drop a file, not a folder.");

        // The file exists, so the file type filter is what turned it down:
        if (paths.Any(File.Exists))
            return TB("Please drop a file with a supported file type.");

        return TB("The dropped file could not be accessed. Please choose it with the file chooser instead.");
    }
}