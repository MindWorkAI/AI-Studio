using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static partial class SourceExtensions
{
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
        var match = MarkdownLinkWithOptionalSuffix().Match(value);
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
    /// Sorts a list of sources into the groups it is shown in, and numbers them.
    /// </summary>
    /// <remarks>
    /// The order of the groups and the running number are what a reader follows, and they have to
    /// be the same wherever the list appears: in the chat, in an exported document, and in the
    /// clipboard. This is why both the chat and the Markdown below ask here instead of sorting the
    /// list themselves.
    /// </remarks>
    /// <param name="sources">The list of sources to sort.</param>
    /// <returns>The groups which have sources, in the order they are shown; empty when there are none.</returns>
    public static IReadOnlyList<SourceGroup> GroupSources(this IList<Source> sources)
    {
        var llmSources = new List<Source>();
        var toolSources = new List<Source>();
        var ragSources = new List<Source>();
        foreach (var source in sources)
        {
            switch (source.Origin)
            {
                case SourceOrigin.LLM:
                    llmSources.Add(source);
                    break;

                case SourceOrigin.TOOL:
                    toolSources.Add(source);
                    break;

                case SourceOrigin.RAG:
                    ragSources.Add(source);
                    break;
            }
        }

        var groups = new List<SourceGroup>(3);
        var sourceNum = 0;
        AddGroup(groups, TB("Sources provided by the AI"), llmSources, ref sourceNum);
        AddGroup(groups, TB("Sources used by tools"), toolSources, ref sourceNum);
        AddGroup(groups, TB("Sources provided by the data providers"), ragSources, ref sourceNum);
        return groups;
    }

    private static void AddGroup(ICollection<SourceGroup> groups, string heading, IReadOnlyList<Source> sources, ref int sourceNum)
    {
        if (sources.Count == 0)
            return;

        var numberedSources = new List<NumberedSource>(sources.Count);
        foreach (var source in sources)
            numberedSources.Add(new(++sourceNum, source));

        groups.Add(new(heading, numberedSources));
    }

    /// <summary>
    /// Converts a list of sources to a markdown-formatted string.
    /// </summary>
    /// <param name="sources">The list of sources to convert.</param>
    /// <param name="keepPageAnchors">Whether a link into a local file may name its page; see the method below.</param>
    /// <returns>A markdown-formatted string representing the sources.</returns>
    public static string ToMarkdown(this IList<Source> sources, bool keepPageAnchors = true)
    {
        var sb = new StringBuilder();
        foreach (var group in sources.GroupSources())
        {
            if (sb.Length > 0)
                sb.AppendLine();

            sb.Append("## ");
            sb.AppendLine(group.Heading);

            foreach (var numberedSource in group.Sources)
            {
                var url = keepPageAnchors ? numberedSource.Source.URL : WithoutPageAnchor(numberedSource.Source.URL);
                sb.Append($"- [{numberedSource.Number}] ");
                AppendMarkdownLink(sb, numberedSource.Source.Title, url);
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Takes the page off a link into a local file, for a reader which cannot follow it.
    /// </summary>
    /// <remarks>
    /// Everything a local link carries in its fragment is dropped, not only a page: a chunk is no
    /// use to any reader either, and what breaks such a link is the fragment itself rather than what
    /// stands in it. A web address keeps its fragment untouched, because there the fragment is part
    /// of the address and naming a section of a page is exactly what it is for.
    /// </remarks>
    /// <param name="url">The link of the source.</param>
    /// <returns>The link without its fragment, or the link itself when it carries none.</returns>
    private static string WithoutPageAnchor(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var cleanedUrl = url.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (!Uri.TryCreate(cleanedUrl, UriKind.Absolute, out var absoluteUri) || !absoluteUri.IsFile || absoluteUri.Fragment.Length == 0)
            return url;

        return absoluteUri.GetComponents(UriComponents.AbsoluteUri & ~UriComponents.Fragment, UriFormat.UriEscaped);
    }

    /// <summary>
    /// Converts a list of sources to a markdown-formatted string, headed by a title of its own.
    /// </summary>
    /// <remarks>
    /// The chat shows the sources in a box below the answer, so the reader sees where the one ends
    /// and the others begin. An exported document is one text: without a heading of its own, the
    /// source list would read like one more section the model wrote. This is why the export asks
    /// for this and the chat does not.
    /// </remarks>
    /// <param name="sources">The list of sources to convert.</param>
    /// <param name="keepPageAnchors">Whether a link into a local file may name its page.</param>
    /// <returns>A markdown-formatted string representing the sources, or an empty string when there are none.</returns>
    public static string ToExportMarkdown(this IList<Source> sources, bool keepPageAnchors = true)
    {
        var sourcesMarkdown = sources.ToMarkdown(keepPageAnchors);
        if (string.IsNullOrWhiteSpace(sourcesMarkdown))
            return string.Empty;

        return $"# {TB("Sources")}{Environment.NewLine}{Environment.NewLine}{sourcesMarkdown}";
    }

    /// <summary>
    /// Reads which document a source names, and which page of it.
    /// </summary>
    /// <remarks>
    /// Only a source which names a file has such a location; a web source is opened by the browser
    /// and never asks. The page rides in the fragment of the link as `page=N`, which is what the PDF
    /// open parameters call for. A chat written before v26.9.1 carries `chunk=N` instead, which names
    /// nothing a program could be sent to: such a source keeps its document and loses only the page.
    /// </remarks>
    /// <param name="source">The source to read.</param>
    /// <param name="location">The document and its page, or the default when the source names no file.</param>
    /// <returns>Whether the source names a file.</returns>
    public static bool TryGetDocumentLocation(this ISource source, out SourceDocumentLocation location)
    {
        location = default;
        if (string.IsNullOrWhiteSpace(source.URL))
            return false;

        var cleanedUrl = source.URL.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (!Uri.TryCreate(cleanedUrl, UriKind.Absolute, out var absoluteUri) || !absoluteUri.IsFile)
            return false;

        //
        // The link was made from a path of this system, so reading it back gives that path again --
        // percent-encoded spaces and umlauts included, and with the separators this system uses.
        //
        var path = absoluteUri.LocalPath;
        if (string.IsNullOrWhiteSpace(path))
            return false;

        location = new(path, ReadPageFromFragment(absoluteUri.Fragment));
        return true;
    }

    private static int? ReadPageFromFragment(string fragment)
    {
        const string PAGE_PARAMETER = "page=";
        foreach (var parameter in fragment.TrimStart('#').Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!parameter.StartsWith(PAGE_PARAMETER, StringComparison.OrdinalIgnoreCase))
                continue;

            if (int.TryParse(parameter.AsSpan(PAGE_PARAMETER.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var pageNumber) && pageNumber > 0)
                return pageNumber;
        }

        return null;
    }

    /// <summary>
    /// Merges a list of added sources into an existing list of sources, avoiding duplicates based on normalized URLs.
    /// </summary>
    /// <param name="sources">The existing list of sources to merge into.</param>
    /// <param name="addedSources">The list of sources to add.</param>
    public static void MergeSources(this IList<Source> sources, IEnumerable<ISource> addedSources)
    {
        var sourceIdentities = sources
            .Select(source => GetSourceIdentity(source.URL))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var addedSource in addedSources)
        {
            if (sourceIdentities.Add(GetSourceIdentity(addedSource.URL)))
                sources.Add((Source)addedSource);
        }
    }

    private static string GetSourceIdentity(string url)
    {
        var cleanedUrl = url.Trim().Replace("\r", string.Empty).Replace("\n", string.Empty);
        if (!Uri.TryCreate(cleanedUrl, UriKind.Absolute, out var absoluteUri))
            return cleanedUrl;

        var normalizedUri = new UriBuilder(absoluteUri)
        {
            Scheme = absoluteUri.Scheme.ToLowerInvariant(),
            Host = absoluteUri.IdnHost.TrimEnd('.').ToLowerInvariant(),
            Port = absoluteUri.IsDefaultPort ? -1 : absoluteUri.Port,
            Fragment = string.Empty,
        };
        return normalizedUri.Uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
    }

    [GeneratedRegex(@"^\[(?<label>[^\]]+)\]\((?<url>[^)\r\n]+)\)(?<suffix>.*)$")]
    private static partial Regex MarkdownLinkWithOptionalSuffix();
}
