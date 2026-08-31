using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace AIStudio.Tools;

/// <summary>
/// Turns an SVG icon which came from outside the app into a data URL we can put into an img tag.
/// </summary>
/// <remarks>
/// <para>
/// Icons from plugins are never rendered inline. They go into an img element instead, where the
/// browser treats the SVG as a standalone document: it runs no script, fires no event handlers, and
/// loads nothing from the network. That is what makes a plugin-supplied icon harmless, so this
/// class does not try to filter active content out of the markup.
/// </para>
/// <para>
/// What is left to do is a size limit and the question of whether the icon is an SVG at all. The
/// latter is a diagnostic rather than a defense: it turns a broken icon into a log message and a
/// fallback instead of a broken image in the UI.
/// </para>
/// </remarks>
internal static class SvgIcon
{
    /// <summary>
    /// The largest icon we accept.
    /// </summary>
    /// <remarks>
    /// Provider icons are persisted into the settings file as a data URL, so an oversized icon
    /// would be written and parsed again on every single settings store. A logo needs far less.
    /// </remarks>
    public const int MAX_ICON_SIZE_BYTES = 32 * 1024;

    private const string SVG_NAMESPACE = "http://www.w3.org/2000/svg";
    private const string DATA_URL_PREFIX = "data:image/svg+xml;base64,";

    private static readonly XNamespace SVG = SVG_NAMESPACE;

    /// <summary>
    /// Validates the given SVG and converts it into a data URL.
    /// </summary>
    /// <param name="svg">The SVG markup to convert.</param>
    /// <param name="dataUrl">The resulting data URL, or an empty string when the icon was rejected.</param>
    /// <param name="issue">The reason why the icon was rejected, or an empty string on success.</param>
    /// <returns>True, when the icon could be converted.</returns>
    public static bool TryCreateDataUrl(string svg, out string dataUrl, out string issue) => TryCreateDataUrl(Encoding.UTF8.GetBytes(svg), out dataUrl, out issue);

    /// <inheritdoc cref="TryCreateDataUrl(string,out string,out string)"/>
    public static bool TryCreateDataUrl(byte[] svg, out string dataUrl, out string issue)
    {
        dataUrl = string.Empty;
        issue = string.Empty;

        if (svg.Length is <= 0 or > MAX_ICON_SIZE_BYTES)
        {
            issue = $"The icon must be between 1 byte and {MAX_ICON_SIZE_BYTES / 1024} KiB.";
            return false;
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MAX_ICON_SIZE_BYTES,
                XmlResolver = null,
            };

            using var stream = new MemoryStream(svg, writable: false);
            using var xmlReader = XmlReader.Create(stream, settings);
            document = XDocument.Load(xmlReader, LoadOptions.None);
        }
        catch (Exception e)
        {
            issue = $"The icon is not valid SVG XML: {e.Message}";
            return false;
        }

        if (document.Root is null || !string.Equals(document.Root.Name.LocalName, "svg", StringComparison.OrdinalIgnoreCase))
        {
            issue = "The icon does not have an SVG root element.";
            return false;
        }

        //
        // An SVG which is meant to be pasted into HTML often omits the namespace, because the HTML
        // parser supplies it. A standalone document inside an img tag has no such help and would
        // not render at all, so we add the namespace instead of rejecting such an icon:
        //
        if (document.Root.Name.Namespace == XNamespace.None)
        {
            // Materialize before renaming: we are about to change the very tree we walk.
            foreach (var element in document.Root.DescendantsAndSelf().ToList())
                if (element.Name.Namespace == XNamespace.None)
                    element.Name = SVG + element.Name.LocalName;

            dataUrl = ToDataUrl(Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting)));
            return true;
        }

        if (document.Root.Name.Namespace != SVG)
        {
            issue = "The icon root element is not in the SVG namespace.";
            return false;
        }

        dataUrl = ToDataUrl(svg);
        return true;
    }

    private static string ToDataUrl(byte[] svg) => $"{DATA_URL_PREFIX}{Convert.ToBase64String(svg)}";
}