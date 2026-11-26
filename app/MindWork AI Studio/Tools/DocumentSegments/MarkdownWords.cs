using System.Text;
using System.Text.RegularExpressions;

namespace AIStudio.Tools.DocumentSegments;

/// <summary>
/// Makes new words fit into a Markdown file, so that they stay words.
/// </summary>
/// <remarks>
/// <para>
/// The words of a Markdown file reach the model as they read, i.e., with their escapes and entities decoded, see
/// MarkdownSegmentFormat; the model never sees markup. So the new words need escapes where Markdown would read them
/// as markup:
/// </para>
/// <list type="bullet">
/// <item>Characters which start or end inline markup, e.g., * of emphasis, [ of a link, | of a table cell, or $ of a
/// formula, get a backslash. Markdown shows an escaped character as the character itself, wherever it stands.</item>
/// <item>Words which would start a block at the start of a line, e.g., "1. January" as a numbered list, "- " as a
/// list, "#" as a heading, or "&gt;" as a quote, get a backslash before that mark.</item>
/// <item>An ampersand which would start an entity, e.g., &amp;copy;, becomes &amp;amp;.</item>
/// <item>Line breaks become spaces: a blank line would end the paragraph or the table row.</item>
/// </list>
/// </remarks>
internal static partial class MarkdownWords
{
    /// <summary>
    /// The characters which start or end inline markup of Markdig with the extensions the format reads, e.g.,
    /// emphasis, links, code, tables, formulas, strikethrough, and superscript.
    /// </summary>
    private const string INLINE_MARKUP = "\\`*_[]<|$~^";

    [GeneratedRegex(@"\A(?<indent>[ \t]*)(?<mark>[-+>]|#{1,6}|\d{1,9}(?=[.)]))(?<rest>[.)]?)(?=[ \t]|\z|(?<=>))")]
    private static partial Regex BlockStartRegex();

    [GeneratedRegex(@"&(?=#?[A-Za-z0-9]+;)")]
    private static partial Regex EntityStartRegex();

    /// <summary>
    /// Makes new words fit into a Markdown file.
    /// </summary>
    /// <param name="words">The new words, as the model wrote them.</param>
    public static string Encoded(string words)
    {
        var result = new StringBuilder(words.Length + 8);
        foreach (var character in words.ReplaceLineEndings(" "))
        {
            if (INLINE_MARKUP.Contains(character))
                result.Append('\\');

            result.Append(character);
        }

        var encoded = EntityStartRegex().Replace(result.ToString(), "&amp;");
        return BlockStartRegex().Replace(encoded, match => match.Groups["mark"].Value.All(char.IsAsciiDigit)
            ? $"{match.Groups["indent"].Value}{match.Groups["mark"].Value}\\{match.Groups["rest"].Value}"
            : $"{match.Groups["indent"].Value}\\{match.Groups["mark"].Value}{match.Groups["rest"].Value}");
    }
}