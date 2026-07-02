using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class SourceExtensions
{
    private static readonly Regex MARKDOWN_LINK_WITH_OPTIONAL_SUFFIX = new(@"^\[(?<label>[^\]]+)\]\((?<url>[^)\r\n]+)\)(?<suffix>.*)$", RegexOptions.Compiled);

    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourceExtensions).Namespace, nameof(SourceExtensions));

    private static void AppendMarkdownLink(StringBuilder sb, string title, string url)
    {
        sb.Append('[');
        sb.Append(EscapeMarkdownLinkText(title));
        sb.Append("](<");
        sb.Append(NormalizeLinkDestination(url));
        sb.Append(">)");
    }

    private static string EscapeMarkdownLinkText(string text)
    {
        return text
            .Replace(@"\", @"\\")
            .Replace("[", @"\[")
            .Replace("]", @"\]")
            .Replace("\r", " ")
            .Replace("\n", " ");
    }

    private static string NormalizeLinkDestination(string url)
    {
        var normalized = url.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        normalized = TryUnwrapMarkdownLink(normalized);

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);

        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (IsSafeUrlCharacter(c))
            {
                sb.Append(c);
                continue;
            }

            sb.Append(Uri.EscapeDataString(c.ToString()));
        }

        return sb.ToString();
    }

    private static string TryUnwrapMarkdownLink(string value)
    {
        var match = MARKDOWN_LINK_WITH_OPTIONAL_SUFFIX.Match(value);
        if (!match.Success)
            return value;

        var label = match.Groups["label"].Value;
        var url = match.Groups["url"].Value;
        var suffix = match.Groups["suffix"].Value;
        if (string.IsNullOrEmpty(suffix))
            return url;

        if (Uri.TryCreate(label, UriKind.Absolute, out var labelUri) &&
            Uri.TryCreate(url, UriKind.Absolute, out var urlUri) &&
            Uri.Compare(labelUri, urlUri, UriComponents.AbsoluteUri, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
            return url + suffix;

        return value;
    }

    private static bool IsSafeUrlCharacter(char c)
    {
        if (char.IsAsciiLetterOrDigit(c))
            return true;

        return c is '-' or '.' or '_' or '~' or ':' or '/' or '?' or '#' or '[' or ']' or '@' or '!' or '$' or '&' or '\'' or '(' or ')' or '*' or '+' or ',' or ';' or '=';
    }
    
    /// <summary>
    /// Converts a list of sources to a markdown-formatted string.
    /// </summary>
    /// <param name="sources">The list of sources to convert.</param>
    /// <returns>A markdown-formatted string representing the sources.</returns>
    public static string ToMarkdown(this IList<Source> sources)
    {
        var sb = new StringBuilder();
        var ragSources = new List<ISource>();
        var sourceNum = 0;
        var addedLLMHeaders = false;
        foreach (var source in sources)
        {
            switch (source.Origin)
            {
                case SourceOrigin.RAG: 
                    ragSources.Add(source);
                    break;
                
                case SourceOrigin.LLM:
                    if (!addedLLMHeaders)
                    {
                        sb.Append("## ");
                        sb.AppendLine(TB("Sources provided by the AI"));
                        addedLLMHeaders = true;
                    }
                    
                    sb.Append($"- [{++sourceNum}] ");
                    AppendMarkdownLink(sb, source.Title, source.URL);
                    sb.AppendLine();
                    break;
            }
        }
        
        if(ragSources.Count == 0)
            return sb.ToString();
        
        sb.AppendLine();
        sb.Append("## ");
        sb.AppendLine(TB("Sources provided by the data providers"));
        
        foreach (var source in ragSources)
        {
            sb.Append($"- [{++sourceNum}] ");
            AppendMarkdownLink(sb, source.Title, source.URL);
            sb.AppendLine();
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Merges a list of added sources into an existing list of sources, avoiding duplicates based on URL and Title.
    /// </summary>
    /// <param name="sources">The existing list of sources to merge into.</param>
    /// <param name="addedSources">The list of sources to add.</param>
    public static void MergeSources(this IList<Source> sources, IList<ISource> addedSources)
    {
        foreach (var addedSource in addedSources)
            if (sources.All(s => s.URL != addedSource.URL && s.Title != addedSource.Title))
                sources.Add((Source)addedSource);
    }
}
