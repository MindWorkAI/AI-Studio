namespace AIStudio.Tools.PluginSystem;

/// <summary>
/// Resolves the file paths a configuration plugin names, e.g. for the attachments of a chat template.
/// </summary>
public static class PluginFilePaths
{
    /// <summary>
    /// Resolves a file path from a configuration plugin.
    /// </summary>
    /// <remarks>
    /// An absolute path is taken as it is: an organization may point to a share every user can read. A
    /// relative path belongs to a file shipped with the plugin, so it must stay inside the plugin folder.
    /// Otherwise, a plugin could make the app read and send any file the user can access.
    /// </remarks>
    /// <param name="filePath">The path as the plugin states it.</param>
    /// <param name="pluginPath">The folder of the plugin.</param>
    /// <param name="resolvedFilePath">The full path to use; the given path when it cannot be used.</param>
    /// <returns>Why the path cannot be used, or <see cref="PluginFilePathIssue.NONE"/>.</returns>
    public static PluginFilePathIssue TryResolve(string filePath, string pluginPath, out string resolvedFilePath)
    {
        resolvedFilePath = filePath;
        if (string.IsNullOrWhiteSpace(filePath))
            return PluginFilePathIssue.EMPTY;

        if (Path.IsPathFullyQualified(filePath))
            return PluginFilePathIssue.NONE;

        if (string.IsNullOrWhiteSpace(pluginPath))
            return PluginFilePathIssue.PLUGIN_PATH_UNKNOWN;

        var pluginRoot = Path.GetFullPath(pluginPath);
        var relativePath = filePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            return PluginFilePathIssue.PARENT_SEGMENT;

        var combinedPath = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        if (!PathTools.IsInsideDirectory(pluginRoot, combinedPath))
            return PluginFilePathIssue.OUTSIDE_PLUGIN_FOLDER;

        resolvedFilePath = combinedPath;
        return PluginFilePathIssue.NONE;
    }
}