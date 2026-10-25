namespace AIStudio.Settings;

/// <summary>
/// Resolves a file or folder a configuration plugin names in a setting, such as the PowerPoint
/// template or the picture library of the Slide Assistant, or the icon of a provider.
/// </summary>
/// <remarks>
/// A plugin may ship the file or folder itself, next to its plugin.lua, and name it by a path
/// relative to its directory. Such a path has to stay inside that directory, so a plugin cannot reach
/// an arbitrary file elsewhere on the system through it. An absolute path, e.g., on a network share,
/// stays as the organization wrote it, where the setting allows one.
/// </remarks>
internal static class PluginFilePath
{
    /// <summary>
    /// Resolves a path against the directory of the plugin which states it.
    /// </summary>
    /// <param name="path">The path as the plugin states it: empty, absolute, or relative to the plugin directory.</param>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <param name="resolvedPath">The full path; empty for an empty path, which means no file.</param>
    /// <param name="issue">Why the path cannot be used; empty on success.</param>
    /// <returns>Whether the path can be used.</returns>
    public static bool TryResolve(string path, string pluginPath, out string resolvedPath, out string issue)
    {
        resolvedPath = string.Empty;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return true;

        if (Path.IsPathFullyQualified(path))
        {
            resolvedPath = path;
            return true;
        }

        return TryResolveInside(path, pluginPath, out resolvedPath, out issue);
    }

    /// <summary>
    /// Resolves the path of a folder against the directory of the plugin which states it.
    /// </summary>
    /// <param name="path">The path as the plugin states it: empty, absolute, or relative to the plugin directory.</param>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <param name="resolvedPath">The full path; empty for an empty path, which means no folder.</param>
    /// <param name="issue">Why the path cannot be used; empty on success.</param>
    /// <returns>Whether the path can be used.</returns>
    public static bool TryResolveDirectory(string path, string pluginPath, out string resolvedPath, out string issue)
    {
        resolvedPath = string.Empty;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
            return true;

        if (Path.IsPathFullyQualified(path))
        {
            resolvedPath = path;
            return true;
        }

        return TryResolveInside(path, pluginPath, true, out resolvedPath, out issue);
    }

    /// <summary>
    /// Resolves a path relative to the directory of a plugin, which has to name an existing file
    /// inside that directory.
    /// </summary>
    /// <param name="relativePath">The path relative to the plugin directory; both kinds of slashes work.</param>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <param name="fullPath">The full path of the file; empty when the path cannot be used.</param>
    /// <param name="issue">Why the path cannot be used; empty on success.</param>
    /// <returns>Whether the path names a file inside the plugin directory.</returns>
    public static bool TryResolveInside(string relativePath, string pluginPath, out string fullPath, out string issue) => TryResolveInside(relativePath, pluginPath, false, out fullPath, out issue);

    /// <summary>
    /// Resolves a path relative to the directory of a plugin, which has to name an existing file or
    /// folder inside that directory.
    /// </summary>
    /// <remarks>
    /// Every file and folder a plugin ships itself is checked here, so that all of them follow the
    /// same rules: no '..' segments, and no link along the path which leads out of the plugin directory.
    /// </remarks>
    /// <param name="relativePath">The path relative to the plugin directory; both kinds of slashes work.</param>
    /// <param name="pluginPath">The directory of the plugin.</param>
    /// <param name="isDirectory">Whether the path has to name a folder rather than a file.</param>
    /// <param name="fullPath">The full path; empty when the path cannot be used.</param>
    /// <param name="issue">Why the path cannot be used; empty on success.</param>
    /// <returns>Whether the path names a file or folder inside the plugin directory.</returns>
    private static bool TryResolveInside(string relativePath, string pluginPath, bool isDirectory, out string fullPath, out string issue)
    {
        fullPath = string.Empty;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            issue = "The relative path cannot be resolved because the plugin directory is unknown.";
            return false;
        }

        var normalizedPath = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (normalizedPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            issue = "The relative path must not contain '..' path segments.";
            return false;
        }

        string pluginRoot;
        string candidatePath;
        try
        {
            pluginRoot = Path.GetFullPath(pluginPath);
            candidatePath = Path.GetFullPath(Path.Combine(pluginRoot, normalizedPath));
        }
        catch (Exception e)
        {
            issue = $"The relative path is invalid: {e.Message}";
            return false;
        }

        if (!PathTools.IsInsideDirectory(pluginRoot, candidatePath))
        {
            issue = "The relative path points outside of the plugin directory.";
            return false;
        }

        if (isDirectory ? !Directory.Exists(candidatePath) : !File.Exists(candidatePath))
        {
            issue = isDirectory ? "The folder does not exist in the plugin directory." : "The file does not exist in the plugin directory.";
            return false;
        }

        try
        {
            if (!LinksStayInsideDirectory(pluginRoot, candidatePath))
            {
                issue = "The relative path contains a link that points outside of the plugin directory.";
                return false;
            }
        }
        catch (Exception e)
        {
            issue = $"The links of the relative path cannot be checked: {e.Message}";
            return false;
        }

        fullPath = candidatePath;
        return true;
    }

    /// <summary>
    /// Whether no link along the path leads out of the root directory.
    /// </summary>
    private static bool LinksStayInsideDirectory(string rootDirectory, string path)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, path);
        var currentPath = Path.GetFullPath(rootDirectory);
        foreach (var segment in relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo pathInfo = Directory.Exists(currentPath) ? new DirectoryInfo(currentPath) : new FileInfo(currentPath);
            var finalTarget = pathInfo.ResolveLinkTarget(true);
            if (finalTarget is not null && !PathTools.IsInsideDirectory(rootDirectory, finalTarget.FullName))
                return false;
        }

        return true;
    }
}