using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Web;
using HtmlAgilityPack;

// See WebPageContentExtractor: the attribute indexer and ParentNode return null in practice.
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

namespace AIStudio.Tools.Web;

/// <summary>
/// Turns documents which a page embeds, such as a PDF in a viewer frame, into links.
/// </summary>
/// <remarks>
/// The extraction removes frames, objects, and embeds, because what they usually hold, such as
/// videos, ads, or maps, is no text of the page. An intranet page, however, often shows its forms,
/// guidelines, and slides exactly that way, and the document is then the actual content of the page.
/// Without its address in the page, the model could neither name it nor open it: with the free
/// address choice switched off, only an address which stands in the chat is read.<br/><br/>
/// Only an element which recognizably holds a document becomes a link, by its declared media type or
/// by the file name of its address. A viewer, such as PDF.js, names the document in a parameter of
/// its own address; a parameter which holds an address or an absolute path is followed then, since
/// the viewer page itself has no text to read. Every other element is removed as before.
/// </remarks>
internal static class EmbeddedDocumentLinks
{
    private const int MAX_LINK_TEXT_CHARACTERS = 200;

    private static readonly HashSet<string> DOCUMENT_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".odt", ".pptx", ".odp", ".xlsx", ".xlsm", ".xls", ".ods",
    };

    private static readonly HashSet<string> DOCUMENT_MEDIA_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.presentation",
        "application/vnd.oasis.opendocument.spreadsheet",
        "application/vnd.ms-excel",
    };

    /// <summary>
    /// Replaces every frame, object, and embed holding a document with a link to it.
    /// </summary>
    /// <param name="root">The node whose descendants are replaced.</param>
    /// <param name="baseUrl">The address relative ones are resolved against.</param>
    public static void ReplaceWithLinks(HtmlNode root, Uri baseUrl)
    {
        foreach (var node in root.Descendants().Where(node => node.Name is "iframe" or "embed" or "object").Reverse().ToList())
        {
            if (node.ParentNode is not { } parent || FindDocumentUrl(node, baseUrl) is not { } documentUrl)
                continue;

            var link = HtmlNode.CreateNode("<p><a></a></p>");
            var anchor = link.FirstChild;
            // Escaped, because ToString() would turn %20 back into a space, which ends an address:
            anchor.SetAttributeValue("href", documentUrl.AbsoluteUri);
            anchor.InnerHtml = WebUtility.HtmlEncode($"Embedded document: {GetLinkText(node, documentUrl)}");
            parent.ReplaceChild(link, node);
        }
    }

    /// <summary>
    /// The address of the document an element embeds, or null when it embeds none.
    /// </summary>
    internal static Uri? FindDocumentUrl(HtmlNode node, Uri baseUrl)
    {
        var source = WebUtility.HtmlDecode(node.GetAttributeValue(node.Name is "object" ? "data" : "src", string.Empty)).Trim();
        if (source.Length is 0 || HtmlContentRules.IsScriptOrDataUrl(source) || !TryResolve(baseUrl, source, out var url))
            return null;

        if (DOCUMENT_MEDIA_TYPES.Contains(node.GetAttributeValue("type", string.Empty).Trim()) || NamesDocument(url.AbsolutePath))
            return url;

        // A viewer names the document in a parameter, e.g. viewer.html?file=/files/guideline.pdf:
        var parameters = HttpUtility.ParseQueryString(url.Query);
        foreach (var key in parameters.AllKeys)
        {
            foreach (var value in parameters.GetValues(key) ?? [])
            {
                var candidate = value.Trim();
                var isAddress = candidate.StartsWith('/') || candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
                if (isAddress && TryResolve(url, candidate, out var documentUrl) && NamesDocument(documentUrl.AbsolutePath))
                    return documentUrl;
            }
        }

        return null;
    }

    private static bool NamesDocument(string path) => DOCUMENT_EXTENSIONS.Contains(Path.GetExtension(Uri.UnescapeDataString(path)));

    private static bool TryResolve(Uri baseUrl, string value, [NotNullWhen(true)] out Uri? url) =>
        Uri.TryCreate(baseUrl, value, out url) && url is { Scheme: "http" or "https" };

    private static string GetLinkText(HtmlNode node, Uri documentUrl)
    {
        var label = new[] { node.GetAttributeValue("title", string.Empty), node.GetAttributeValue("aria-label", string.Empty), node.GetAttributeValue("name", string.Empty) }
            .Select(value => string.Join(' ', WebUtility.HtmlDecode(value).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .FirstOrDefault(value => value.Length > 0);

        label ??= Path.GetFileName(Uri.UnescapeDataString(documentUrl.AbsolutePath));
        return label.Length <= MAX_LINK_TEXT_CHARACTERS ? label : label[..MAX_LINK_TEXT_CHARACTERS].TrimEnd();
    }
}