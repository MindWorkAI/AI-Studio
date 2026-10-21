using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools.RAG;

public static class SourcePassageExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(SourcePassageExtensions).Namespace, nameof(SourcePassageExtensions));

    /// <summary>
    /// Whether the passage comes from a transcript, e.g., of an audio or a video file.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>True for a transcript.</returns>
    public static bool IsTranscript(this SourcePassage passage) => passage.Type is RetrievalContentType.TEXT_TRANSCRIPT;

    /// <summary>
    /// Names the page or the pages the passage is on, e.g., "Page 4" or "Pages 4–5".
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The location, or an empty string when the passage has no page.</returns>
    public static string GetLocationLabel(this SourcePassage passage) => GetLocationLabel([passage]);

    /// <summary>
    /// Names the pages several passages are on, e.g., "Page 4", "Pages 4–5", or "Pages 1, 11".
    /// </summary>
    /// <remarks>
    /// The pages follow the order of the document, and each passage names its pages once: two
    /// passages on the same page name that page once. An end page which is not after the start page
    /// names no range, so the passage is on one page. A passage without a page, such as a slide or a
    /// sheet, adds nothing, since no page is invented for it.
    /// </remarks>
    /// <param name="passages">The passages.</param>
    /// <returns>The location, or an empty string when no passage has a page.</returns>
    public static string GetLocationLabel(this IEnumerable<SourcePassage> passages)
    {
        var pageRanges = passages
            .Where(passage => passage.PageNumber is > 0)
            .Select(passage => (First: passage.PageNumber!.Value, Last: Math.Max(passage.PageNumber!.Value, passage.EndPageNumber ?? 0)))
            .Distinct()
            .Order()
            .ToList();

        return pageRanges switch
        {
            [] => string.Empty,
            [var onePage] when onePage.First == onePage.Last => string.Format(TB("Page {0}"), onePage.First),
            _ => string.Format(TB("Pages {0}"), string.Join(", ", pageRanges.Select(range => range.First == range.Last ? $"{range.First}" : $"{range.First}–{range.Last}"))),
        };
    }

    /// <summary>
    /// Names in the title of each source where its passages are, e.g., "handbook.pdf (Pages 1, 11)".
    /// </summary>
    /// <remarks>
    /// The list of sources keeps one entry per document, see SourceExtensions.MergeSources, and its
    /// title came from the passage found first, e.g., "handbook.pdf (Page 1)". A second passage on
    /// page 11 would go unmentioned, so the title is made anew from all passages of the source.
    /// Passages without a page, e.g., of a text file, are counted instead as soon as there are
    /// several of them. A source with one passage without a page keeps the title its data source
    /// gave it, and so does every source without passages, such as a web page.
    /// </remarks>
    /// <param name="sources">The sources of an answer.</param>
    /// <param name="passages">The passages of the same answer.</param>
    public static void NamePassagesInTitles(this IList<Source> sources, IReadOnlyCollection<SourcePassage> passages)
    {
        if (passages.Count is 0)
            return;

        var passagesBySource = passages.ToLookup(passage => SourceExtensions.GetSourceIdentity(passage.SourceUrl), StringComparer.Ordinal);
        for (var index = 0; index < sources.Count; index++)
        {
            var passagesOfSource = passagesBySource[SourceExtensions.GetSourceIdentity(sources[index].URL)].ToList();
            if (passagesOfSource.Count is 0 || string.IsNullOrWhiteSpace(passagesOfSource[0].DataSourceName))
                continue;

            var location = passagesOfSource.GetLocationLabel();
            if (location.Length is 0 && passagesOfSource.Count > 1)
                location = string.Format(TB("{0} passages"), passagesOfSource.Count);

            if (location.Length is 0)
                continue;

            sources[index] = sources[index] with { Title = $"{passagesOfSource[0].DataSourceName} ({location})" };
        }
    }

    /// <summary>
    /// Merges added passages into an existing list of passages, leaving out those it holds already.
    /// </summary>
    /// <remarks>
    /// A passage is known when one with the same address and the same text is, e.g., the same chunk
    /// found by two searches for one answer. Unlike the sources, the address counts with its page:
    /// the same sentence on two pages of a document is two passages.
    /// </remarks>
    /// <param name="passages">The existing list of passages to merge into.</param>
    /// <param name="addedPassages">The passages to add.</param>
    public static void MergePassages(this IList<SourcePassage> passages, IEnumerable<SourcePassage> addedPassages)
    {
        var knownPassages = passages
            .Select(passage => (passage.SourceUrl, passage.Text))
            .ToHashSet();

        foreach (var addedPassage in addedPassages)
        {
            if (knownPassages.Add((addedPassage.SourceUrl, addedPassage.Text)))
                passages.Add(addedPassage);
        }
    }

    /// <summary>
    /// Finds the passages listed under a source.
    /// </summary>
    /// <remarks>
    /// The list of sources keeps one entry per document, whichever page its link names, see
    /// SourceExtensions.MergeSources. The passages are matched the same way, so the entry of a
    /// document shows the passages of all its pages, and every passage shows up under its source.
    /// </remarks>
    /// <param name="passages">The passages of an answer.</param>
    /// <param name="source">The source.</param>
    /// <returns>The passages of the source, in their order; empty when it has none.</returns>
    public static IReadOnlyList<SourcePassage> GetPassagesOf(this IEnumerable<SourcePassage> passages, ISource source)
    {
        var sourceIdentity = SourceExtensions.GetSourceIdentity(source.URL);
        return passages
            .Where(passage => string.Equals(SourceExtensions.GetSourceIdentity(passage.SourceUrl), sourceIdentity, StringComparison.Ordinal))
            .ToList();
    }
}