using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// A paragraph of a Word document as a segment, with all the writer needs to put it together again.
/// </summary>
/// <param name="Paragraph">The paragraph.</param>
/// <param name="Segment">Its words with tags.</param>
/// <param name="Pairs">What each pair of tags stands for.</param>
/// <param name="Placeholders">What each tag of its own stands for.</param>
/// <param name="TextFormats">The properties of the runs with words within each pair, and with 0 outside of any pair: the most common formatting there, or the formatting of a pair of formatting.</param>
/// <param name="StartMarkers">The starts of bookmarks, comments, and the like, which go before the words.</param>
/// <param name="EndMarkers">The ends of bookmarks, comments, and the like, which go after the words.</param>
/// <param name="HasMixedFormatting">Whether runs with words within the same pair differ in their formatting, which the new words do not keep; only when the formatting changes too often for pairs of formatting.</param>
internal sealed record WordParagraph(
    W.Paragraph Paragraph,
    DocumentSegment Segment,
    IReadOnlyDictionary<int, WordPair> Pairs,
    IReadOnlyDictionary<int, WordAtom> Placeholders,
    IReadOnlyDictionary<int, W.RunProperties?> TextFormats,
    IReadOnlyList<OpenXmlElement> StartMarkers,
    IReadOnlyList<OpenXmlElement> EndMarkers,
    bool HasMixedFormatting)
{
    /// <summary>
    /// All elements the tags stand for, in the order they stood in the paragraph.
    /// </summary>
    public IEnumerable<WordAtom> AtomsInOrder => this.Placeholders.Values
        .Concat(this.Pairs.Values.OfType<WordFieldPair>().SelectMany(pair => new[] { pair.Opening, pair.Closing }))
        .OrderBy(atom => atom.Order);

    /// <summary>
    /// The properties for new words within a pair, or outside of any pair for 0.
    /// </summary>
    /// <remarks>
    /// Words within a pair without words of its own, e.g., when the model moved them there, get the
    /// properties of the words outside of any pair, or else of the first words of the paragraph.
    /// </remarks>
    /// <param name="pairId">The number of the innermost pair; 0 for none.</param>
    public W.RunProperties? TextFormatOf(int pairId)
    {
        if (this.TextFormats.TryGetValue(pairId, out var format) || this.TextFormats.TryGetValue(0, out format))
            return format;

        return this.TextFormats.Values.FirstOrDefault();
    }
}