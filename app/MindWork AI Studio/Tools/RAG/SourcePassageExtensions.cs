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
    /// <remarks>
    /// An end page which is not after the start page names no range, so the passage is on one page.
    /// </remarks>
    /// <param name="passage">The passage.</param>
    /// <returns>The location, or an empty string when the passage has no page.</returns>
    public static string GetLocationLabel(this SourcePassage passage) => passage switch
    {
        { PageNumber: int startPage and > 0, EndPageNumber: int endPage } when endPage > startPage => string.Format(TB("Pages {0}–{1}"), startPage, endPage),
        { PageNumber: int page and > 0 } => string.Format(TB("Page {0}"), page),
        _ => string.Empty,
    };

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