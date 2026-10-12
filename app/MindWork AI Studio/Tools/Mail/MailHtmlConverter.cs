using System.Globalization;
using System.Net;

using AIStudio.Tools.Web;

using HtmlAgilityPack;

namespace AIStudio.Tools.Mail;

/// <summary>
/// Turns the HTML part of a mail into the Markdown a reader of that mail would see.
/// </summary>
/// <remarks>
/// A mail comes from a stranger, and HTML lets the stranger write text nobody sees but every model
/// reads. Before the conversion, everything goes which the reader would not see or which does
/// something on its own:
/// <list type="bullet">
///   <item>scripts, styles, comments and embedded objects;</item>
///   <item>hidden elements, by the rules web pages are read with (HtmlContentRules) and those only
///   a mail needs: a mail runs no scripts, so text with font-size:0 or opacity:0, or squeezed into
///   max-height:0 with overflow:hidden, never becomes visible;</item>
///   <item>every image, since the user did not ask to load any;</item>
///   <item>links which run code or carry data of their own (javascript:, vbscript:, data:), so only
///   their text remains.</item>
/// </list>
/// Tables which only lay out a newsletter turn into plain blocks, see IsLayoutTable. The
/// conversion resolves the HTML entities, so the prompt injection filter reads the text the way
/// the reader does.
/// </remarks>
public static class MailHtmlConverter
{
    private static readonly HashSet<string> REMOVED_ELEMENT_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "title", "meta", "link", "base", "script", "style", "noscript", "template", "iframe", "frame", "frameset",
        "object", "embed", "applet", "canvas", "svg", "math", "img", "picture", "video", "audio", "source", "track", "map", "area",
        "button", "input", "select", "textarea", "colgroup", "col"
    };

    /// <summary>
    /// The parts of a table. A table within a table is not one of them: it is judged on its own.
    /// </summary>
    private static readonly HashSet<string> TABLE_PART_NAMES = new(StringComparer.OrdinalIgnoreCase)
    {
        "thead", "tbody", "tfoot", "tr", "td", "th", "caption"
    };

    private static readonly string[] URL_ATTRIBUTE_NAMES = ["href", "src", "action", "formaction", "background", "poster", "xlink:href"];

    /// <summary>
    /// Converts the HTML part of a mail.
    /// </summary>
    /// <param name="html">The HTML part, already decoded from its transfer encoding and charset.</param>
    /// <returns>The Markdown, empty when nothing visible remains.</returns>
    public static string ToMarkdown(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        var document = new HtmlDocument();
        document.LoadHtml(html);

        // HtmlAgilityPack annotates SelectSingleNode as never returning null, which it does without a body:
        // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
        var root = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;

        RemoveInvisibleNodes(root);
        RemoveScriptAndDataUrls(root);
        FlattenLayoutTables(root);

        string markdown;
        try
        {
            markdown = HTMLParser.ParseToMarkdown(root.InnerHtml);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            //
            // What the converter fails on depends on the HTML it got, and a mail should not lose its
            // text over it. Everything hidden is gone from the tree already, so its plain text is
            // what the reader sees, merely without the formatting:
            //
            markdown = WebUtility.HtmlDecode(root.InnerText);
        }

        return MailTextNormalization.NormalizeBody(markdown);
    }

    private static void RemoveInvisibleNodes(HtmlNode root)
    {
        var invisibleNodes = root.Descendants()
            .Where(node => node.NodeType is HtmlNodeType.Comment || REMOVED_ELEMENT_NAMES.Contains(node.Name) || HtmlContentRules.IsHiddenByMarkup(node) || IsHiddenInMail(node))
            .Reverse()
            .ToList();

        foreach (var node in invisibleNodes)
            node.Remove();
    }

    /// <summary>
    /// Whether an inline style hides an element in a mail, beyond what hides it on a web page as well.
    /// </summary>
    private static bool IsHiddenInMail(HtmlNode node)
    {
        var style = HtmlContentRules.GetInlineStyle(node);
        if (style.Count is 0)
            return false;

        // Outlook's own switch for hiding an element:
        if (style.GetValueOrDefault("mso-hide") is "all")
            return true;

        if (IsZeroLength(style.GetValueOrDefault("font-size")) || IsZeroLength(style.GetValueOrDefault("opacity")))
            return true;

        return style.GetValueOrDefault("overflow") is "hidden" && (IsZeroLength(style.GetValueOrDefault("max-height")) || IsZeroLength(style.GetValueOrDefault("height")));
    }

    /// <summary>
    /// Whether a style value is zero, whatever its unit: 0, 0px, 0.0em and 0% all are.
    /// </summary>
    private static bool IsZeroLength(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        var numberLength = 0;
        while (numberLength < value.Length && (char.IsAsciiDigit(value[numberLength]) || value[numberLength] is '.' or '+' or '-'))
            numberLength++;

        return numberLength > 0 && double.TryParse(value.AsSpan(0, numberLength), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == 0;
    }

    private static void RemoveScriptAndDataUrls(HtmlNode root)
    {
        foreach (var node in root.DescendantsAndSelf())
        {
            foreach (var attributeName in URL_ATTRIBUTE_NAMES)
            {
                if (HtmlContentRules.IsScriptOrDataUrl(node.GetAttributeValue(attributeName, string.Empty)))
                    node.Attributes.Remove(attributeName);
            }
        }
    }

    /// <summary>
    /// Turns the tables which only arrange a newsletter into plain blocks, cell by cell.
    /// </summary>
    /// <remarks>
    /// Mail programs know little CSS, so newsletters build their whole layout from tables nested in
    /// tables. Converted as tables, each would become a Markdown table with the rest of the mail in
    /// its cells, which costs tokens and tells nothing. A table which holds figures stays a table.
    /// </remarks>
    private static void FlattenLayoutTables(HtmlNode root)
    {
        // Decided for all tables before the first one changes, since a table holding another one is a layout table:
        var layoutTables = root.Descendants("table").Where(IsLayoutTable).ToList();
        foreach (var table in layoutTables)
        {
            foreach (var node in table.Descendants().Where(node => TABLE_PART_NAMES.Contains(node.Name) && OwningTable(node) == table).ToList())
                node.Name = "div";

            table.Name = "div";
        }
    }

    /// <summary>
    /// Whether a table only arranges the content around it: it says so (role=presentation), it
    /// holds another table, or none of its rows has more than one cell.
    /// </summary>
    private static bool IsLayoutTable(HtmlNode table)
    {
        if (table.GetAttributeValue("role", string.Empty).Equals("presentation", StringComparison.OrdinalIgnoreCase))
            return true;

        var ownNodes = table.Descendants().Where(node => OwningTable(node) == table).ToList();
        if (ownNodes.Any(node => node.Name.Equals("table", StringComparison.OrdinalIgnoreCase)))
            return true;

        return ownNodes
            .Where(node => node.Name.Equals("tr", StringComparison.OrdinalIgnoreCase))
            .All(row => row.ChildNodes.Count(cell => cell.Name is "td" or "th") <= 1);
    }

    /// <summary>
    /// The closest table above a node, i.e., the table a row or cell belongs to.
    /// </summary>
    private static HtmlNode? OwningTable(HtmlNode node) => node.Ancestors().FirstOrDefault(ancestor => ancestor.Name.Equals("table", StringComparison.OrdinalIgnoreCase));
}