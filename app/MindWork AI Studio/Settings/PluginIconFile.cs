namespace AIStudio.Settings;

/// <summary>
/// Loads an icon file a configuration plugin points to, such as a custom provider logo.
/// </summary>
/// <remarks>
/// The checks here are about the path, not about the markup: they keep a plugin from turning an
/// arbitrary file somewhere on the system into a data URL. Whether the file is a usable SVG, and
/// how it becomes a data URL, is up to SvgIcon.
/// </remarks>
internal static class PluginIconFile
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

        if (!string.Equals(Path.GetExtension(iconPath), ".svg", StringComparison.OrdinalIgnoreCase))
        {
            issue = "The icon file must use the .svg extension.";
            return false;
        }

        // The same rules as for every other file a plugin ships:
        if (!PluginFilePath.TryResolveInside(iconPath, pluginPath, out var resolvedPath, out issue))
            return false;

        try
        {
            // Check the size before reading, so an oversized file never reaches memory:
            var fileInfo = new FileInfo(resolvedPath);
            if (fileInfo.Length is <= 0 or > SvgIcon.MAX_ICON_SIZE_BYTES)
            {
                issue = $"The icon file must be between 1 byte and {SvgIcon.MAX_ICON_SIZE_BYTES / 1024} KiB.";
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
}