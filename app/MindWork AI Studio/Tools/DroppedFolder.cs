namespace AIStudio.Tools;

/// <summary>
/// Picks the folder which a picker for exactly one folder takes from the paths dropped onto it.
/// </summary>
public static class DroppedFolder
{
    private static string TB(string fallbackEN) => PluginSystem.I18N.I.T(fallbackEN, typeof(DroppedFolder).Namespace, nameof(DroppedFolder));

    /// <summary>
    /// Finds the first dropped path which leads to a folder.
    /// </summary>
    /// <remarks>
    /// A picker carries exactly one folder, so a multi-selection cannot be honored as a whole. A
    /// dropped file is rejected instead of being taken as its parent folder. Everything a folder
    /// contains is processed, so guessing the parent of a mistakenly dropped file could pull in far
    /// more data than the user meant to hand over.
    /// </remarks>
    /// <param name="paths">The dropped paths, in the order the runtime delivered them.</param>
    /// <returns>The path of the folder to take, or null when no dropped path leads to a folder.</returns>
    public static string? FirstUsable(IReadOnlyList<string> paths) => paths.FirstOrDefault(Directory.Exists);

    /// <summary>
    /// Explains why none of the dropped paths could be used.
    /// </summary>
    /// <param name="paths">The dropped paths.</param>
    public static string WarningFor(IReadOnlyList<string> paths)
    {
        // Naming the actual mistake beats a generic "that did not work". Dropping a file onto a
        // folder picker is the likeliest of them:
        if (paths.Any(File.Exists))
            return TB("Please drop a folder, not a file.");

        return TB("The dropped folder could not be accessed. Please choose it with the folder chooser instead.");
    }
}