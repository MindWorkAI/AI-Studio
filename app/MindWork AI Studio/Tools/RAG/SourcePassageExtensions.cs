using AIStudio.Tools.Databases.ProvenanceStore;
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
    /// What sort of content the passage is, as the provenance store files it.
    /// </summary>
    /// <remarks>
    /// Told by what the content is rather than by where it came from: a mail is a mail, whether a
    /// mailbox data source or a tool handed it over.
    /// </remarks>
    /// <param name="passage">The passage.</param>
    /// <returns>The kind of its entry.</returns>
    public static ProvenanceKind GetProvenanceKind(this SourcePassage passage) => passage.Type switch
    {
        RetrievalContentType.TEXT_EMAIL => ProvenanceKind.MAIL,
        RetrievalContentType.TEXT_WEBSITE => ProvenanceKind.WEB_PAGE,
        _ => ProvenanceKind.DATA_SOURCE_PASSAGE,
    };

    /// <summary>
    /// Makes the passage of a web page which a tool read for the AI, so that the user can read under
    /// the source of the page what the AI received from it.
    /// </summary>
    /// <remarks>
    /// Listed under the source by its address, see GetPassagesOf, and named by its title in the
    /// dialog of its passages. A page says nothing about pages or lines, so its source keeps the
    /// title the tool gave it, see NamePassagesInTitles.
    /// </remarks>
    /// <param name="source">The source of the page, as the tool lists it.</param>
    /// <param name="content">The content of the page as the model received it: filtered for prompt injections, and cut where the tool cut it.</param>
    /// <returns>The passage of the page.</returns>
    public static SourcePassage ToWebPagePassage(this Source source, string content) => new()
    {
        SourceUrl = source.URL,
        DataSourceName = source.Title,
        Path = source.URL,
        Category = RetrievalContentCategory.TEXT,
        Type = RetrievalContentType.TEXT_WEBSITE,
        Text = content,
    };

    /// <summary>
    /// Whether the passage holds texts which belong into the provenance store, see SourcePassage.ProvenanceId.
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>False once the texts are stored, and for a passage without any, such as an image.</returns>
    public static bool HoldsTextsToStore(this SourcePassage passage) => passage.ProvenanceId is null && (passage.Text.Length > 0 || passage.SurroundingContent.Count > 0);

    /// <summary>
    /// What stands between the sheets of a spreadsheet in a location, e.g., "Budget: rows 3–9 · Summary: row 2".
    /// </summary>
    private const string SHEET_SEPARATOR = " · ";

    /// <summary>
    /// Names where the passage is in its document, e.g., "Page 4", "Pages 4–5", "Budget: rows 3–9", "Lines 40–85", or "Part 3".
    /// </summary>
    /// <param name="passage">The passage.</param>
    /// <returns>The location, or an empty string when the data source does not say.</returns>
    public static string GetLocationLabel(this SourcePassage passage) => GetLocationLabel([passage]);

    /// <summary>
    /// Where the passage is in its document, as numbers only, e.g., its pages 4–5, its lines 40–85, or its part 3.
    /// </summary>
    /// <remarks>
    /// Orders the passages of one document, in the order of the document. The passages of the
    /// sheets of a spreadsheet give their parts, since rows start anew in every sheet.
    /// </remarks>
    /// <param name="passage">The passage.</param>
    /// <returns>The numbers, or null when the data source does not say.</returns>
    public static LocationRange? GetLocationRange(this SourcePassage passage) => GetLocation([passage])?.Ranges[0];

    /// <summary>
    /// Names where several passages are in their document, e.g., "Pages 1, 11", "Budget: rows 3–9 · Summary: row 2", "Lines 40–85, 120" or "Parts 2, 5".
    /// </summary>
    /// <param name="passages">The passages.</param>
    /// <returns>The location, or an empty string when the data source does not say.</returns>
    public static string GetLocationLabel(this IEnumerable<SourcePassage> passages)
    {
        var passagesOfDocument = passages.ToList();
        if (passagesOfDocument.All(passage => passage.PageNumber is not > 0) && GetSheetsLabel(passagesOfDocument) is { } sheets)
            return sheets;

        return passagesOfDocument.GetLocation()?.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Finds where several passages are in their document, in parts, so that each page can be shown
    /// as a link.
    /// </summary>
    /// <remarks>
    /// A document with pages is named by its pages: they are what the user finds again in the
    /// document, and what a program can open it at. A table without sheets, such as a CSV file, is
    /// named by its rows, and a text by its lines. Rows and lines are nothing a program can open a
    /// document at, so they become no links. A document which says none of them is named by the
    /// parts it was split into for the search. Either way, the numbers follow the order of the
    /// document, each once: two passages on the same page name that page once. An end which is not
    /// after the start names no range.<br/><br/>
    /// The rows of a spreadsheet start anew in every sheet, so they need the name of their sheet
    /// and are named by GetLocationLabel, sheet by sheet. Here, its passages give their parts.
    /// </remarks>
    /// <param name="passages">The passages.</param>
    /// <returns>The location, or null when the data source says nothing about it.</returns>
    public static PassageLocation? GetLocation(this IEnumerable<SourcePassage> passages)
    {
        var passagesOfDocument = passages.ToList();
        var pages = passagesOfDocument
            .Where(passage => passage.PageNumber is > 0)
            .Select(passage => RangeOf(passage.PageNumber!.Value, passage.EndPageNumber))
            .ToList();

        if (pages.Count > 0)
            return CreateLocation(pages, TB("Page {0}"), TB("Pages {0}"), arePages: true);

        var rows = passagesOfDocument
            .Where(passage => passage is { StartRow: > 0, SheetName: null or "" })
            .Select(passage => RangeOf(passage.StartRow!.Value, passage.EndRow))
            .ToList();

        if (rows.Count > 0)
            return CreateLocation(rows, TB("Row {0}"), TB("Rows {0}"), arePages: false);

        var lines = passagesOfDocument
            .Where(passage => passage.StartLine is > 0)
            .Select(passage => RangeOf(passage.StartLine!.Value, passage.EndLine))
            .ToList();

        if (lines.Count > 0)
            return CreateLocation(lines, TB("Line {0}"), TB("Lines {0}"), arePages: false);

        var parts = passagesOfDocument
            .Where(passage => passage.ChunkNumber is > 0)
            .Select(passage => new LocationRange(passage.ChunkNumber!.Value, passage.ChunkNumber!.Value))
            .ToList();

        return parts.Count > 0 ? CreateLocation(parts, TB("Part {0}"), TB("Parts {0}"), arePages: false) : null;
    }

    /// <summary>
    /// Names the rows of passages from the sheets of a spreadsheet, sheet by sheet, e.g., "Budget: rows 3–9, 20 · Summary: row 2".
    /// </summary>
    /// <remarks>
    /// The sheets follow the order of the document, which is the order of the parts the passages
    /// are. A passage never runs from one sheet into the next, cf. TextChunker.SplitBySheets.
    /// </remarks>
    /// <param name="passages">The passages of one document.</param>
    /// <returns>The location, or null when no passage names a sheet.</returns>
    private static string? GetSheetsLabel(IReadOnlyList<SourcePassage> passages)
    {
        var sheets = passages
            .Where(passage => passage is { StartRow: > 0, SheetName.Length: > 0 })
            .OrderBy(passage => passage.ChunkNumber ?? int.MaxValue)
            .GroupBy(passage => passage.SheetName!, StringComparer.Ordinal)
            .Select(sheet => GetSheetLabel(sheet.Key, sheet.Select(passage => RangeOf(passage.StartRow!.Value, passage.EndRow))))
            .ToList();

        return sheets.Count > 0 ? string.Join(SHEET_SEPARATOR, sheets) : null;
    }

    private static string GetSheetLabel(string sheetName, IEnumerable<LocationRange> rows)
    {
        var orderedRows = rows
            .Distinct()
            .OrderBy(range => range.First)
            .ThenBy(range => range.Last)
            .ToList();

        var format = orderedRows is [var single] && single.First == single.Last ? TB("{0}: row {1}") : TB("{0}: rows {1}");
        return string.Format(format, sheetName, string.Join(PassageLocation.RANGE_SEPARATOR, orderedRows));
    }

    /// <summary>
    /// The numbers from a start to an end; an end which is not after the start names no range.
    /// </summary>
    private static LocationRange RangeOf(int first, int? last) => new(first, Math.Max(first, last ?? 0));

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
    /// spreadsheet is named by its sheets and rows the same way, a text file by its lines, see
    /// GetLocationLabel. Passages whose data source
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
    /// the same sentence on two pages of a document is two passages. A stored passage is told
    /// apart by its entry, since its text lies in the provenance store.
    /// </remarks>
    /// <param name="passages">The existing list of passages to merge into.</param>
    /// <param name="addedPassages">The passages to add.</param>
    public static void MergePassages(this IList<SourcePassage> passages, IEnumerable<SourcePassage> addedPassages)
    {
        var knownPassages = passages
            .Select(passage => (passage.SourceUrl, passage.Text, passage.ProvenanceId))
            .ToHashSet();

        foreach (var addedPassage in addedPassages)
        {
            if (knownPassages.Add((addedPassage.SourceUrl, addedPassage.Text, addedPassage.ProvenanceId)))
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