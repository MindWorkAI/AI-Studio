using System.Xml;
using System.Xml.Linq;

namespace AIStudio.Settings;

internal static class ProviderIconFile
{
    private const int MAX_FILE_SIZE_BYTES = 256 * 1024;
    private static readonly HashSet<string> FORBIDDEN_ELEMENTS = new(StringComparer.OrdinalIgnoreCase)
    {
        "embed",
        "discard",
        "foreignObject",
        "iframe",
        "object",
        "script",
        "set",
        "style",
        "animate",
        "animateMotion",
        "animateTransform",
    };

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
            var fileInfo = new FileInfo(resolvedPath);
            var finalTarget = fileInfo.ResolveLinkTarget(true);
            if (finalTarget is not null && !IsInsideDirectory(pluginRoot, finalTarget.FullName))
            {
                issue = "The icon file link points outside of the configuration plugin directory.";
                return false;
            }

            if (fileInfo.Length is <= 0 or > MAX_FILE_SIZE_BYTES)
            {
                issue = $"The icon file must be between 1 byte and {MAX_FILE_SIZE_BYTES / 1024} KiB.";
                return false;
            }

            if (!LinksStayInsideDirectory(pluginRoot, resolvedPath))
            {
                issue = "The icon path contains a link that points outside of the configuration plugin directory.";
                return false;
            }

            var bytes = File.ReadAllBytes(resolvedPath);
            if (bytes.Length is <= 0 or > MAX_FILE_SIZE_BYTES)
            {
                issue = $"The icon file must be between 1 byte and {MAX_FILE_SIZE_BYTES / 1024} KiB.";
                return false;
            }

            if (!TryValidateSvg(bytes, out issue))
                return false;

            dataUrl = $"data:image/svg+xml;base64,{Convert.ToBase64String(bytes)}";
            return true;
        }
        catch (Exception e)
        {
            issue = $"The icon file could not be read: {e.Message}";
            return false;
        }
    }

    private static bool TryValidateSvg(byte[] svg, out string issue)
    {
        issue = string.Empty;
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MAX_FILE_SIZE_BYTES,
                XmlResolver = null,
            };
            using var stream = new MemoryStream(svg, writable: false);
            using var xmlReader = XmlReader.Create(stream, settings);
            document = XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (Exception e)
        {
            issue = $"The icon file is not valid SVG XML: {e.Message}";
            return false;
        }

        if (document.Root is null ||
            !string.Equals(document.Root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase) ||
            document.Root.Name.NamespaceName != "http://www.w3.org/2000/svg")
        {
            issue = "The icon file does not have an SVG root element in the SVG namespace.";
            return false;
        }

        if (document.DescendantNodes().OfType<XProcessingInstruction>().Any())
        {
            issue = "The icon file must not contain processing instructions.";
            return false;
        }

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            if (FORBIDDEN_ELEMENTS.Contains(element.Name.LocalName))
            {
                issue = $"The icon file contains the forbidden SVG element '{element.Name.LocalName}'.";
                return false;
            }

            foreach (var attribute in element.Attributes())
            {
                if (attribute.Name == XNamespace.Xml + "base")
                {
                    issue = $"The icon file contains the forbidden SVG attribute '{attribute.Name.LocalName}'.";
                    return false;
                }

                if (attribute.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                {
                    issue = $"The icon file contains the forbidden event-handler attribute '{attribute.Name.LocalName}'.";
                    return false;
                }

                if (attribute.Name.LocalName is "href" or "src")
                {
                    var reference = attribute.Value.Trim();
                    if (!string.IsNullOrEmpty(reference) && !reference.StartsWith('#'))
                    {
                        issue = "The icon file contains an external resource reference.";
                        return false;
                    }
                }

                if (ContainsExternalCssUrl(attribute.Value))
                {
                    issue = "The icon file contains an external CSS resource reference.";
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ContainsExternalCssUrl(string value)
    {
        var searchStart = 0;
        while (true)
        {
            var urlStart = value.IndexOf("url(", searchStart, StringComparison.OrdinalIgnoreCase);
            if (urlStart < 0)
                return false;

            var valueStart = urlStart + 4;
            var valueEnd = value.IndexOf(')', valueStart);
            if (valueEnd < 0)
                return true;

            var reference = value[valueStart..valueEnd].Trim().Trim('\'', '"');
            if (!reference.StartsWith('#'))
                return true;

            searchStart = valueEnd + 1;
        }
    }

    private static bool LinksStayInsideDirectory(string rootDirectory, string filePath)
    {
        var relativePath = Path.GetRelativePath(rootDirectory, filePath);
        var currentPath = Path.GetFullPath(rootDirectory);
        foreach (var segment in relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
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
