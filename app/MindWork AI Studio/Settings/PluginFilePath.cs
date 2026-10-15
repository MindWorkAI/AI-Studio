namespace AIStudio.Settings;

/// <summary>
/// Resolves a file a configuration plugin names in a setting, such as the PowerPoint template of the
/// Slide Planner.
/// </summary>
/// <remarks>
/// A plugin may ship the file itself, next to its plugin.lua, and name it by a path relative to its
/// directory. Such a path has to stay inside that directory, so a plugin cannot reach an arbitrary
/// file elsewhere on the system through it. An absolute path, e.g., on a network share, stays as
/// the organization wrote it.
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

        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            issue = "The relative path cannot be resolved because the plugin directory is unknown.";
            return false;
        }

        var relativePath = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            issue = "The relative path must not contain '..' path segments.";
            return false;
        }

        string pluginRoot;
        string fullPath;
        try
        {
            pluginRoot = Path.GetFullPath(pluginPath);
            fullPath = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        }
        catch (Exception e)
        {
            issue = $"The relative path is invalid: {e.Message}";
            return false;
        }

        if (!IsInsideDirectory(pluginRoot, fullPath))
        {
            issue = "The relative path points outside of the plugin directory.";
            return false;
        }

        if (!File.Exists(fullPath))
        {
            issue = "The file does not exist in the plugin directory.";
            return false;
        }

        try
        {
            if (!LinksStayInsideDirectory(pluginRoot, fullPath))
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

        resolvedPath = fullPath;
        return true;
    }

    /// <summary>
    /// Whether no link along the path leads out of the root directory.
    /// </summary>
    internal static bool LinksStayInsideDirectory(string rootDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, filePath);
        var currentPath = Path.GetFullPath(rootDirectory);
        foreach (var segment in relativePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            currentPath = Path.Combine(currentPath, segment);
            FileSystemInfo pathInfo = Directory.Exists(currentPath) ? new DirectoryInfo(currentPath) : new FileInfo(currentPath);
            var finalTarget = pathInfo.ResolveLinkTarget(true);
            if (finalTarget is not null && !IsInsideDirectory(rootDirectory, finalTarget.FullName))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the path lies inside the root directory.
    /// </summary>
    internal static bool IsInsideDirectory(string rootDirectory, string path)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return target.StartsWith(root, comparison);
    }
}