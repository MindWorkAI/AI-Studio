using System.Net;
using HtmlAgilityPack;

namespace AIStudio.Tools.Web;

/// <summary>
/// The rules for HTML from strangers which hold for web pages and for mails alike.
/// </summary>
/// <remarks>
/// Kept in one place so the two cannot drift apart: what counts as hidden and which links run code
/// is no different in a newsletter than on the page it links to. What differs stays with each of
/// them, e.g. that a mail may hide text by turning it transparent, which a web page does all the
/// time while its scripts fade the text in.
/// </remarks>
internal static class HtmlContentRules
{
    private static readonly IReadOnlyDictionary<string, string> EMPTY_STYLE = new Dictionary<string, string>();

    /// <summary>
    /// Whether the markup of an element hides it from the reader.
    /// </summary>
    /// <remarks>
    /// That is the hidden attribute, aria-hidden, and an inline style with display:none or
    /// visibility:hidden. A style sheet hiding the element by its class is not seen here.
    /// </remarks>
    /// <param name="node">The element.</param>
    /// <returns>True when the element is hidden.</returns>
    public static bool IsHiddenByMarkup(HtmlNode node)
    {
        if (node.Attributes.Contains("hidden") ||
            node.GetAttributeValue("aria-hidden", string.Empty).Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;

        var style = GetInlineStyle(node);
        return style.GetValueOrDefault("display") is "none" || style.GetValueOrDefault("visibility") is "hidden" or "collapse";
    }

    /// <summary>
    /// Reads the inline style of an element.
    /// </summary>
    /// <remarks>
    /// Names and values are lowercase and without any whitespace, and an !important is dropped:
    /// "Display : NONE !important" reads as display = none. A property set twice keeps the last
    /// value, as it does in a browser.
    /// </remarks>
    /// <param name="node">The element.</param>
    /// <returns>The declarations by property name, empty when the element has no style.</returns>
    public static IReadOnlyDictionary<string, string> GetInlineStyle(HtmlNode node)
    {
        var style = WebUtility.HtmlDecode(node.GetAttributeValue("style", string.Empty));
        if (string.IsNullOrWhiteSpace(style))
            return EMPTY_STYLE;

        var declarations = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var declaration in style.Split(';'))
        {
            var separatorIndex = declaration.IndexOf(':');
            if (separatorIndex <= 0)
                continue;

            var name = WithoutWhitespace(declaration[..separatorIndex]);
            var value = WithoutWhitespace(declaration[(separatorIndex + 1)..]);
            if (value.EndsWith("!important", StringComparison.Ordinal))
                value = value[..^"!important".Length];

            if (name.Length > 0)
                declarations[name] = value;
        }

        return declarations;
    }

    /// <summary>
    /// Whether a link or a source runs code or carries content of its own instead of pointing somewhere.
    /// </summary>
    /// <remarks>
    /// That is javascript:, vbscript: and data:. A browser skips tabs and line breaks inside the
    /// scheme, so "java&amp;#9;script:" runs just as well, and those are skipped here, too.
    /// </remarks>
    /// <param name="url">The value of the attribute, as it stands in the HTML.</param>
    /// <returns>True when the attribute has to go.</returns>
    public static bool IsScriptOrDataUrl(string url)
    {
        var value = string.Concat(WebUtility.HtmlDecode(url).Where(character => !char.IsWhiteSpace(character) && !char.IsControl(character)));
        return value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static string WithoutWhitespace(string value) => string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();
}