using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using W = DocumentFormat.OpenXml.Wordprocessing;
using W14 = DocumentFormat.OpenXml.Office2010.Word;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Reads the paragraphs of a Word document as segments: of the body, the tables, and the text boxes, of the
/// headers and footers, and of the footnotes and endnotes.
/// </summary>
/// <remarks>
/// <para>
/// Words become text, and elements which enclose runs, e.g., links or content controls, become pairs of tags.
/// A field whose result lies in the same paragraph becomes a pair as well, around its result. Everything else
/// within the paragraph, e.g., tabs, pictures, references to notes, or formulas, becomes a tag of its own.
/// A bookmark around words of the paragraph is a pair as well, see BookmarkPairsOf. Other bookmarks and the ranges
/// of comments are no tags: their starts go before the new words, their ends after them.
/// </para>
/// <para>
/// A paragraph inside another one, e.g., in a text box, is a segment of its own; for the paragraph around it,
/// its text box is a tag of its own. Text boxes also have a copy for older versions of Word, which holds the same
/// words and therefore costs nothing more. Content controls bound to data stay as they are, since Word fills them
/// from their data anyway, and so do those which offer a choice, a date, or a check box.
/// </para>
/// </remarks>
internal static class WordSegmentReader
{
    /// <summary>
    /// Reads the paragraphs which can be segments, in the order of the document.
    /// </summary>
    /// <param name="document">The document.</param>
    public static IReadOnlyList<WordParagraph> Read(WordprocessingDocument document)
    {
        var paragraphs = new List<WordParagraph>();
        foreach (var root in RootsOf(document))
        {
            foreach (var paragraph in root.Descendants<W.Paragraph>())
            {
                if (IsInBoundContentControl(paragraph))
                    continue;

                if (ReadParagraph(paragraph) is { } read)
                    paragraphs.Add(read);
            }
        }

        return paragraphs;
    }

    private static IEnumerable<OpenXmlElement> RootsOf(WordprocessingDocument document)
    {
        if (document.MainDocumentPart is not { } mainPart)
            yield break;

        if (mainPart.Document?.Body is { } body)
            yield return body;

        foreach (var headerPart in mainPart.HeaderParts)
        {
            if (headerPart.Header is { } header)
                yield return header;
        }

        foreach (var footerPart in mainPart.FooterParts)
        {
            if (footerPart.Footer is { } footer)
                yield return footer;
        }

        // The separators of the notes hold no words of their own:
        if (mainPart.FootnotesPart?.Footnotes is { } footnotes)
        {
            foreach (var footnote in footnotes.Elements<W.Footnote>().Where(note => note.Type is null || note.Type.Value == W.FootnoteEndnoteValues.Normal))
                yield return footnote;
        }

        if (mainPart.EndnotesPart?.Endnotes is { } endnotes)
        {
            foreach (var endnote in endnotes.Elements<W.Endnote>().Where(note => note.Type is null || note.Type.Value == W.FootnoteEndnoteValues.Normal))
                yield return endnote;
        }
    }

    /// <summary>
    /// Reads a paragraph, with its changes of formatting as pairs as long as they are few, see FormatSpans.MAX_PAIRS.
    /// </summary>
    private static WordParagraph? ReadParagraph(W.Paragraph paragraph)
    {
        var reader = new ParagraphReader(usesFormatPairs: true);
        var read = reader.TryRead(paragraph);
        return reader.FormatPairCount <= FormatSpans.MAX_PAIRS ? read : new ParagraphReader(usesFormatPairs: false).TryRead(paragraph);
    }

    private static bool IsInBoundContentControl(W.Paragraph paragraph) => paragraph.Ancestors<W.SdtElement>().Any(contentControl => !IsEditable(contentControl));

    /// <summary>
    /// Whether the words of a content control are words of the user rather than data or a choice.
    /// </summary>
    private static bool IsEditable(W.SdtElement contentControl) => contentControl.SdtProperties?.ChildElements.Any(property => property
        is W.DataBinding
        or W.ShowingPlaceholder
        or W.SdtContentDropDownList
        or W.SdtContentComboBox
        or W.SdtContentDate
        or W14.SdtContentCheckBox) is not true;

    /// <summary>
    /// What one element of a paragraph is, before the tags are numbered.
    /// </summary>
    private abstract record Item;

    /// <summary>
    /// Words of a run.
    /// </summary>
    private sealed record TextItem(OpenXmlElement Element, W.RunProperties? Format, string Text) : Item;

    /// <summary>
    /// An element which goes back as it is.
    /// </summary>
    private sealed record PieceItem(WordPiece Piece, PieceKind Kind) : Item;

    /// <summary>
    /// An element which encloses runs, with what it encloses.
    /// </summary>
    private sealed record ContainerItem(OpenXmlElement Container, List<Item> Children) : Item;

    private enum PieceKind
    {
        PLAIN,
        FIELD_BEGIN,
        FIELD_SEPARATE,
        FIELD_END,
        MARKER_START,
        MARKER_END,
        DROPPED,
    }

    /// <summary>
    /// Reads one paragraph.
    /// </summary>
    private sealed class ParagraphReader
    {
        private readonly TaggedTextBuilder builder = new();
        private readonly FormatSpanReader<W.RunProperties> spans;
        private readonly Dictionary<int, WordPair> pairs = [];
        private readonly Dictionary<int, WordAtom> placeholders = [];
        private readonly Dictionary<int, W.RunProperties?> textFormats = [];
        private readonly List<OpenXmlElement> startMarkers = [];
        private readonly List<OpenXmlElement> endMarkers = [];
        private int nextOrder;

        public ParagraphReader(bool usesFormatPairs) => this.spans = new(this.builder, usesFormatPairs, FormatKeyOf);

        /// <summary>
        /// How many changes of formatting became pairs.
        /// </summary>
        public int FormatPairCount => this.spans.FormatPairCount;

        public WordParagraph? TryRead(W.Paragraph paragraph)
        {
            this.Build(ItemsOf(paragraph), 0);

            // Words which read as a tag could not be told apart from the tags of the paragraph:
            if (this.spans.HasTagLikeWords)
                return null;

            // The pairs of formatting have their formatting already, the other pairs and the words outside of any pair get the most common one:
            foreach (var (pairId, format) in this.spans.BaseFormats)
                this.textFormats[pairId] = format;

            return new(paragraph, new DocumentSegment(this.builder.Build()), this.pairs, this.placeholders, this.textFormats, this.startMarkers, this.endMarkers, this.spans.HasMixedFormatting);
        }

        private void Build(List<Item> items, int pairId)
        {
            var baseFormatKey = this.BaseFormatKeyOf(items);
            var bookmarkEnds = BookmarkPairsOf(items);
            var index = 0;
            while (index < items.Count)
            {
                switch (items[index])
                {
                    case TextItem:
                        index = this.BuildWords(items, index, pairId, baseFormatKey, bookmarkEnds);
                        break;

                    case PieceItem { Kind: PieceKind.MARKER_START } when bookmarkEnds.TryGetValue(index, out var bookmarkEnd):
                        index = this.BuildBoundaryPair(items, index, bookmarkEnd);
                        break;

                    case PieceItem { Kind: PieceKind.DROPPED }:
                        index++;
                        break;

                    case PieceItem { Kind: PieceKind.MARKER_START } startMarker:
                        this.startMarkers.Add(startMarker.Piece.Element);
                        index++;
                        break;

                    case PieceItem { Kind: PieceKind.MARKER_END } endMarker:
                        this.endMarkers.Add(endMarker.Piece.Element);
                        index++;
                        break;

                    case PieceItem { Kind: PieceKind.FIELD_BEGIN }:
                        index = this.BuildField(items, index, pairId);
                        break;

                    case ContainerItem container when HasWords(container.Children):
                        var containerPair = this.builder.Open();
                        this.pairs[containerPair] = new WordContainerPair(container.Container);
                        this.Build(container.Children, containerPair);
                        this.builder.Close(containerPair);
                        index++;
                        break;

                    default:
                        this.AddPlaceholder(items, index, index);
                        index++;
                        break;
                }
            }
        }

        /// <summary>
        /// Reads a field which starts at the given item.
        /// </summary>
        /// <returns>The index of the first item after the field.</returns>
        private int BuildField(List<Item> items, int begin, int pairId)
        {
            var (separate, end) = PartsOfField(items, begin);
            switch (separate, end)
            {
                // A field with its result in this paragraph: the result is words within a pair.
                case ({ } separateIndex, { } endIndex):
                    return this.BuildBoundaryPair(items, begin, endIndex, separateIndex);

                // A field without a result, which shows nothing:
                case (null, { } endWithoutResult):
                    this.AddPlaceholder(items, begin, endWithoutResult);
                    return endWithoutResult + 1;

                // A field whose result goes on in the next paragraphs, e.g., a table of contents:
                case ({ } separateWithoutEnd, null):
                    this.AddPlaceholder(items, begin, separateWithoutEnd);
                    return separateWithoutEnd + 1;

                // A field whose instruction goes on in the next paragraph:
                default:
                    this.AddPlaceholder(items, begin, items.Count - 1);
                    return items.Count;
            }
        }

        /// <summary>
        /// Reads words between a start and an end which stay as they are, see WordBoundaryPair.
        /// </summary>
        /// <param name="items">The items of this level.</param>
        /// <param name="start">The first item of the start.</param>
        /// <param name="end">The item of the end.</param>
        /// <param name="startEnd">The last item of the start, e.g., the mark which separates the result of a field; the start itself when it is one item.</param>
        /// <returns>The index of the first item after the end.</returns>
        private int BuildBoundaryPair(List<Item> items, int start, int end, int? startEnd = null)
        {
            var wordsStart = (startEnd ?? start) + 1;
            var opening = this.AtomOf(items, start, wordsStart - 1);
            var pair = this.builder.Open();
            this.Build(items.GetRange(wordsStart, end - wordsStart), pair);
            this.builder.Close(pair);
            this.pairs[pair] = new WordBoundaryPair(opening, this.AtomOf(items, end, end));
            return end + 1;
        }

        /// <summary>
        /// Finds the bookmarks of a level which become pairs around their words.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A cross reference shows the words its bookmark encloses, e.g., the label and the number of a caption. As a
        /// pair, the bookmark encloses the new words of the model alike; as a mark before and after the words, it would
        /// widen to the whole paragraph, and the cross reference would show the whole caption once Word updates it.
        /// </para>
        /// <para>
        /// That takes a bookmark whose start and end lie in the same level, with words in between, and which crosses
        /// neither another such bookmark nor a field: pairs have to nest. Every other bookmark, an empty one such as
        /// the one Word keeps for the last place of editing included, stays a mark, as do the ranges of comments.
        /// </para>
        /// </remarks>
        /// <returns>The index of the end of each such bookmark, by the index of its start.</returns>
        private static Dictionary<int, int> BookmarkPairsOf(List<Item> items)
        {
            var openStarts = new Dictionary<string, int>(StringComparer.Ordinal);
            var candidates = new List<(int Start, int End)>();
            for (var index = 0; index < items.Count; index++)
            {
                switch (items[index])
                {
                    case PieceItem { Kind: PieceKind.MARKER_START, Piece.Element: W.BookmarkStart { Id.Value: { } startId } }:
                        openStarts[startId] = index;
                        break;

                    case PieceItem { Kind: PieceKind.MARKER_END, Piece.Element: W.BookmarkEnd { Id.Value: { } endId } } when openStarts.Remove(endId, out var start):
                        var enclosed = items.GetRange(start + 1, index - start - 1);
                        if (HasWords(enclosed) && FieldsAreWhole(enclosed))
                            candidates.Add((start, index));

                        break;
                }
            }

            // Of two bookmarks which cross, the one which starts first becomes a pair:
            var pairs = new Dictionary<int, int>();
            foreach (var (start, end) in candidates.OrderBy(candidate => candidate.Start))
            {
                if (pairs.All(pair => !Cross(pair.Key, pair.Value, start, end)))
                    pairs[start] = end;
            }

            return pairs;
        }

        private static bool Cross(int firstStart, int firstEnd, int secondStart, int secondEnd) =>
            (firstStart < secondStart && secondStart < firstEnd && firstEnd < secondEnd) ||
            (secondStart < firstStart && firstStart < secondEnd && secondEnd < firstEnd);

        /// <summary>
        /// Whether every field which starts among the items also ends among them, and the other way round.
        /// </summary>
        private static bool FieldsAreWhole(List<Item> items)
        {
            var depth = 0;
            foreach (var item in items)
            {
                if (item is PieceItem { Kind: PieceKind.FIELD_BEGIN })
                    depth++;
                else if (item is PieceItem { Kind: PieceKind.FIELD_END } && --depth < 0)
                    return false;
            }

            return depth == 0;
        }

        /// <summary>
        /// Finds the separator and the end of a field among the items which follow its start, skipping the fields within it.
        /// </summary>
        private static (int? Separate, int? End) PartsOfField(List<Item> items, int begin)
        {
            int? separate = null;
            var depth = 0;
            for (var index = begin + 1; index < items.Count; index++)
            {
                switch (items[index])
                {
                    case PieceItem { Kind: PieceKind.FIELD_BEGIN }:
                        depth++;
                        break;

                    case PieceItem { Kind: PieceKind.FIELD_SEPARATE } when depth == 0:
                        separate ??= index;
                        break;

                    case PieceItem { Kind: PieceKind.FIELD_END } when depth == 0:
                        return (separate, index);

                    case PieceItem { Kind: PieceKind.FIELD_END }:
                        depth--;
                        break;
                }
            }

            return (separate, null);
        }

        /// <summary>
        /// Reads the words which follow each other from the given item on, up to the next tag.
        /// </summary>
        /// <remarks>
        /// Bookmarks and hints for the layout between the runs do not end the words. Where their formatting
        /// changes, the words in the most common formatting of this pair stay words, and those in any other
        /// formatting go into a pair of their own.
        /// </remarks>
        /// <returns>The index of the first item after the words.</returns>
        private int BuildWords(List<Item> items, int start, int pairId, string baseFormatKey, Dictionary<int, int> bookmarkEnds)
        {
            var words = new List<TextItem>();
            var index = start;
            for (; index < items.Count; index++)
            {
                if (items[index] is TextItem text)
                    words.Add(text);
                else if (items[index] is PieceItem { Kind: PieceKind.MARKER_START } startMarker && !bookmarkEnds.ContainsKey(index))
                    this.startMarkers.Add(startMarker.Piece.Element);
                else if (items[index] is PieceItem { Kind: PieceKind.MARKER_END } endMarker)
                    this.endMarkers.Add(endMarker.Piece.Element);
                else if (items[index] is not PieceItem { Kind: PieceKind.DROPPED })
                    break;
            }

            this.spans.AddWords(words.Select(word => (word.Text, word.Format)), pairId, baseFormatKey, (formatPair, format) =>
            {
                this.pairs[formatPair] = new WordFormatPair(format);
                this.textFormats[formatPair] = format;
            });

            return index;
        }

        /// <summary>
        /// Finds the formatting most words of a pair have, or of the paragraph outside of any pair.
        /// </summary>
        private string BaseFormatKeyOf(List<Item> items) => this.spans.BaseKeyOf(items.OfType<TextItem>().Select(word => (word.Text, word.Format)));

        private void AddPlaceholder(List<Item> items, int first, int last)
        {
            var placeholder = this.builder.Placeholder();
            this.placeholders[placeholder] = this.AtomOf(items, first, last);
        }

        private WordAtom AtomOf(List<Item> items, int first, int last)
        {
            var pieces = new List<WordPiece>();
            for (var index = first; index <= last; index++)
            {
                pieces.Add(items[index] switch
                {
                    TextItem text => new WordPiece(text.Element, text.Format, true),
                    PieceItem piece => piece.Piece,
                    ContainerItem container => new WordPiece(container.Container, null, false),

                    _ => throw new InvalidOperationException("A paragraph holds an item of an unknown kind."),
                });
            }

            return new WordAtom(pieces, this.nextOrder++);
        }

        private static bool HasWords(List<Item> items) => items.Any(item => item switch
        {
            TextItem => true,
            ContainerItem container => HasWords(container.Children),

            _ => false,
        });

        /// <summary>
        /// Tells the formatting of runs apart, leaving out what changes nothing about how words look.
        /// </summary>
        private static string FormatKeyOf(W.RunProperties format)
        {
            var copy = (W.RunProperties)format.CloneNode(true);
            copy.Languages = null;
            copy.NoProof = null;

            // A run without anything that changes its look counts as having no properties at all:
            return copy.HasAttributes || copy.HasChildren ? copy.OuterXml : string.Empty;
        }

        private static List<Item> ItemsOf(OpenXmlElement parent)
        {
            var items = new List<Item>();
            foreach (var child in parent.ChildElements)
            {
                switch (child)
                {
                    // The properties of a paragraph, of custom XML, or of a simple field go with their element, see WordParagraphWriter.ShellOf:
                    case W.ParagraphProperties or W.CustomXmlProperties or W.FieldData:
                        break;

                    case W.Run run:
                        AddItemsOfRun(run, items);
                        break;

                    case W.ProofError:
                        items.Add(new PieceItem(new WordPiece(child, null, false), PieceKind.DROPPED));
                        break;

                    case W.BookmarkStart or W.CommentRangeStart or W.PermStart or W.MoveFromRangeStart or W.MoveToRangeStart:
                        items.Add(new PieceItem(new WordPiece(child, null, false), PieceKind.MARKER_START));
                        break;

                    case W.BookmarkEnd or W.CommentRangeEnd or W.PermEnd or W.MoveFromRangeEnd or W.MoveToRangeEnd:
                        items.Add(new PieceItem(new WordPiece(child, null, false), PieceKind.MARKER_END));
                        break;

                    // A simple field encloses its result, like a field of its own whose result lies in the paragraph:
                    case W.Hyperlink or W.InsertedRun or W.MoveToRun or W.CustomXmlRun or W.SimpleField or W.BidirectionalOverride or W.BidirectionalEmbedding:
                        items.Add(new ContainerItem(child, ItemsOf(child)));
                        break;

                    case W.SdtRun { SdtContentRun: { } content } contentControl when IsEditable(contentControl):
                        items.Add(new ContainerItem(child, ItemsOf(content)));
                        break;

                    // Formulas, deleted runs, and whatever else:
                    default:
                        items.Add(new PieceItem(new WordPiece(child, null, false), PieceKind.PLAIN));
                        break;
                }
            }

            return items;
        }

        private static void AddItemsOfRun(W.Run run, List<Item> items)
        {
            var format = run.RunProperties;
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case W.RunProperties:
                        break;

                    case W.Text text:
                        items.Add(new TextItem(child, format, text.Text));
                        break;

                    // Shown as a hyphen at which no line breaks, which the same character carries over:
                    case W.NoBreakHyphen:
                        items.Add(new TextItem(child, format, "‑"));
                        break;

                    // Hints for the layout, which the new words do not need:
                    case W.LastRenderedPageBreak or W.SoftHyphen:
                        items.Add(new PieceItem(new WordPiece(child, format, true), PieceKind.DROPPED));
                        break;

                    case W.FieldChar { FieldCharType: { } type }:
                        items.Add(new PieceItem(new WordPiece(child, format, true), KindOfFieldChar(type.Value)));
                        break;

                    default:
                        items.Add(new PieceItem(new WordPiece(child, format, true), PieceKind.PLAIN));
                        break;
                }
            }
        }

        private static PieceKind KindOfFieldChar(W.FieldCharValues type)
        {
            if (type == W.FieldCharValues.Begin)
                return PieceKind.FIELD_BEGIN;

            if (type == W.FieldCharValues.Separate)
                return PieceKind.FIELD_SEPARATE;

            return type == W.FieldCharValues.End ? PieceKind.FIELD_END : PieceKind.PLAIN;
        }
    }
}