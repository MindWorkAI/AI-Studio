namespace AIStudio.Settings;

/// <summary>
/// Loads the custom provider icon a configuration plugin may point to.
/// </summary>
/// <remarks>
/// The checks here are about the path, not about the markup: they keep a plugin from turning an
/// arbitrary file somewhere on the system into a data URL. Whether the file is a usable SVG, and
/// how it becomes a data URL, is up to SvgIcon.
/// </remarks>
internal static class ProviderIconFile
{
    public static bool TryLoadDataUrl(string iconPath, string pluginPath, out string dataUrl, out string issue)
    {
        dataUrl = string.Empty;
        issue = string.Empty;

        if (string.IsNullOrWhiteSpace(iconPath))
        {
            issue = "The icon path is empty.";
            return false;
        }

        if (Path.IsPathFullyQualified(iconPath))
        {
            issue = "The icon path must be relative to the configuration plugin directory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(pluginPath))
        {
            issue = "The icon path cannot be resolved because the configuration plugin directory is unknown.";
            return false;
        }

        var relativePath = iconPath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (relativePath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
        {
            issue = "The icon path must not contain '..' path segments.";
            return false;
        }

        string pluginRoot;
        string resolvedPath;
        try
        {
            pluginRoot = Path.GetFullPath(pluginPath);
            resolvedPath = Path.GetFullPath(Path.Combine(pluginRoot, relativePath));
        }
        catch (Exception e)
        {
            issue = $"The icon path is invalid: {e.Message}";
            return false;
        }

        if (!IsInsideDirectory(pluginRoot, resolvedPath))
        {
            issue = "The icon path points outside of the configuration plugin directory.";
            return false;
        }

        if (!string.Equals(Path.GetExtension(resolvedPath), ".svg", StringComparison.OrdinalIgnoreCase))
        {
            issue = "The icon file must use the .svg extension.";
            return false;
        }

        if (!File.Exists(resolvedPath))
        {
            issue = "The icon file does not exist.";
            return false;
        }

        try
        {
            // Check the size before reading, so an oversized file never reaches memory:
            var fileInfo = new FileInfo(resolvedPath);
            if (fileInfo.Length is <= 0 or > SvgIcon.MAX_ICON_SIZE_BYTES)
            {
                issue = $"The icon file must be between 1 byte and {SvgIcon.MAX_ICON_SIZE_BYTES / 1024} KiB.";
                return false;
            }

            if (!LinksStayInsideDirectory(pluginRoot, resolvedPath))
            {
                issue = "The icon path contains a link that points outside of the configuration plugin directory.";
                return false;
            }

            return SvgIcon.TryCreateDataUrl(File.ReadAllBytes(resolvedPath), out dataUrl, out issue);
        }
        catch (Exception e)
        {
            issue = $"The icon file could not be read: {e.Message}";
            return false;
        }
    }

    private static bool LinksStayInsideDirectory(string rootDirectory, string filePath)
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

    private static bool IsInsideDirectory(string rootDirectory, string path)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return target.StartsWith(root, comparison);
    }
}