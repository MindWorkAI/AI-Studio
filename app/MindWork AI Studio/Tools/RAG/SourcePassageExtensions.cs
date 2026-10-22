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
    /// Names where the passage is in its document, e.g., "Page 4", "Pages 4–5", or "Part 3".
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The location, or an empty string when the data source does not say.</returns>
    public static string GetLocationLabel(this SourcePassage passage) => GetLocationLabel([passage]);

    /// <summary>
    /// Where the passage is in its document, as numbers only, e.g., its pages 4–5 or its part 3.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The pages or the part, or null when the data source does not say.</returns>
    public static LocationRange? GetLocationRange(this SourcePassage passage) => GetLocation([passage])?.Ranges[0];

    /// <summary>
    /// Names where several passages are in their document, e.g., "Pages 1, 11" or "Parts 2, 5".
    /// </summary>
    /// <param name="passages">The passages.</param>
    /// <returns>The location, or an empty string when the data source does not say.</returns>
    public static string GetLocationLabel(this IEnumerable<SourcePassage> passages) => passages.GetLocation()?.ToString() ?? string.Empty;

    /// <summary>
    /// Finds where several passages are in their document, in parts, so that each page can be shown
    /// as a link.
    /// </summary>
    /// <remarks>
    /// A document with pages is named by its pages: they are what the user finds again in the
    /// document, and what a program can open it at. A document without pages, such as a text file,
    /// is named by the parts it was split into for the search. Either way, the numbers follow the
    /// order of the document, each once: two passages on the same page name that page once. An end
    /// page which is not after the start page names no range, so the passage is on one page.
    /// </remarks>
    /// <param name="passages">The passages.</param>
    /// <returns>The location, or null when the data source says neither pages nor parts.</returns>
    public static PassageLocation? GetLocation(this IEnumerable<SourcePassage> passages)
    {
        var passagesOfDocument = passages.ToList();
        var pages = passagesOfDocument
            .Where(passage => passage.PageNumber is > 0)
            .Select(passage => new LocationRange(passage.PageNumber!.Value, Math.Max(passage.PageNumber!.Value, passage.EndPageNumber ?? 0)))
            .ToList();

        if (pages.Count > 0)
            return CreateLocation(pages, TB("Page {0}"), TB("Pages {0}"), arePages: true);

        var parts = passagesOfDocument
            .Where(passage => passage.ChunkNumber is > 0)
            .Select(passage => new LocationRange(passage.ChunkNumber!.Value, passage.ChunkNumber!.Value))
            .ToList();

        return parts.Count > 0 ? CreateLocation(parts, TB("Part {0}"), TB("Parts {0}"), arePages: false) : null;
    }

    private static PassageLocation CreateLocation(IEnumerable<LocationRange> ranges, string oneFormat, string severalFormat, bool arePages)
    {
        var orderedRanges = ranges
            .Distinct()
            .OrderBy(range => range.First)
            .ThenBy(range => range.Last)
            .ToList();

        //
        // The words around the numbers come from the translation, which may put them before or after
        // the numbers. A translation which lost its placeholder still gets the numbers, after its words:
        //
        const string PLACEHOLDER = "{0}";
        var format = orderedRanges is [var single] && single.First == single.Last ? oneFormat : severalFormat;
        var placeholderAt = format.IndexOf(PLACEHOLDER, StringComparison.Ordinal);
        return placeholderAt < 0
            ? new PassageLocation($"{format} ", orderedRanges, string.Empty, arePages)
            : new PassageLocation(format[..placeholderAt], orderedRanges, format[(placeholderAt + PLACEHOLDER.Length)..], arePages);
    }

    /// <summary>
    /// Names in the title of each source where its passages are, e.g., "handbook.pdf (Pages 1, 11)".
    /// </summary>
    /// <remarks>
    /// The list of sources keeps one entry per document, see SourceExtensions.MergeSources, and its
    /// title came from the passage found first, e.g., "handbook.pdf (Page 1)". A second passage on
    /// page 11 would go unmentioned, so the title is made anew from all passages of the source. A
    /// text file is named by its parts the same way, see GetLocation. Passages whose data source
    /// says neither, e.g., an external one, are counted instead as soon as there are several of
    /// them. Otherwise, a source keeps the title its data source gave it, and so does every source
    /// without passages, such as a web page.
    /// </remarks>
    /// <param name="sources">The sources of an answer.</param>
    /// <param name="passages">The passages of the same answer.</param>
    public static void NamePassagesInTitles(this IList<Source> sources, IReadOnlyCollection<SourcePassage> passages)
    {
        if (passages.Count is 0)
            return;

        var passagesBySource = passages.SortBySource();
        for (var index = 0; index < sources.Count; index++)
        {
            var passagesOfSource = passagesBySource.GetPassagesOf(sources[index]);
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
    public static IReadOnlyList<SourcePassage> GetPassagesOf(this IEnumerable<SourcePassage> passages, ISource source) => passages.SortBySource().GetPassagesOf(source);

    /// <summary>
    /// Sorts passages by the source they are listed under, to find the passages of many sources.
    /// </summary>
    /// <param name="passages">The passages of an answer.</param>
    /// <returns>The passages by the identity of their source, see SourceExtensions.GetSourceIdentity.</returns>
    public static ILookup<string, SourcePassage> SortBySource(this IEnumerable<SourcePassage> passages) => passages.ToLookup(passage => SourceExtensions.GetSourceIdentity(passage.SourceUrl), StringComparer.Ordinal);

    /// <summary>
    /// Finds the passages listed under a source, among passages sorted by their sources.
    /// </summary>
    /// <param name="passagesBySource">The passages of an answer, see SortBySource.</param>
    /// <param name="source">The source.</param>
    /// <returns>The passages of the source, in their order; empty when it has none.</returns>
    public static IReadOnlyList<SourcePassage> GetPassagesOf(this ILookup<string, SourcePassage> passagesBySource, ISource source) => [..passagesBySource[SourceExtensions.GetSourceIdentity(source.URL)]];
}