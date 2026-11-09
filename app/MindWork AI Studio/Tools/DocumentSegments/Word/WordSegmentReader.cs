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
/// Bookmarks and the ranges of comments are no tags: their starts go before the new words, their ends after them.
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

                if (new ParagraphReader().TryRead(paragraph) is { } read)
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
        private readonly Dictionary<int, WordPair> pairs = [];
        private readonly Dictionary<int, WordAtom> placeholders = [];
        private readonly Dictionary<int, W.RunProperties?> textFormats = [];
        private readonly Dictionary<int, string> textFormatKeys = [];
        private readonly List<OpenXmlElement> startMarkers = [];
        private readonly List<OpenXmlElement> endMarkers = [];
        private bool hasTagLikeWords;
        private bool hasMixedFormatting;
        private int nextOrder;

        public WordParagraph? TryRead(W.Paragraph paragraph)
        {
            this.Build(ItemsOf(paragraph), 0);

            // Words which read as a tag could not be told apart from the tags of the paragraph:
            if (this.hasTagLikeWords)
                return null;

            return new(paragraph, new DocumentSegment(this.builder.Build()), this.pairs, this.placeholders, this.textFormats, this.startMarkers, this.endMarkers, this.hasMixedFormatting);
        }

        private void Build(List<Item> items, int pairId)
        {
            var index = 0;
            while (index < items.Count)
            {
                switch (items[index])
                {
                    case TextItem text:
                        this.AddWords(text, pairId);
                        index++;
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
                    var opening = this.AtomOf(items, begin, separateIndex);
                    var fieldPair = this.builder.Open();
                    this.Build(items.GetRange(separateIndex + 1, endIndex - separateIndex - 1), fieldPair);
                    this.builder.Close(fieldPair);
                    this.pairs[fieldPair] = new WordFieldPair(opening, this.AtomOf(items, endIndex, endIndex));
                    return endIndex + 1;

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

        private void AddWords(TextItem text, int pairId)
        {
            this.hasTagLikeWords |= TaggedText.ContainsTagLikeText(text.Text);
            this.builder.Words(text.Text);

            var formatKey = FormatKeyOf(text.Format);
            if (this.textFormatKeys.TryAdd(pairId, formatKey))
                this.textFormats[pairId] = text.Format;
            else if (this.textFormatKeys[pairId] != formatKey && text.Text.Any(character => !char.IsWhiteSpace(character)))
                this.hasMixedFormatting = true;
        }

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
        private static string FormatKeyOf(W.RunProperties? format)
        {
            if (format is null)
                return string.Empty;

            var copy = (W.RunProperties)format.CloneNode(true);
            copy.Languages = null;
            copy.NoProof = null;
            return copy.OuterXml;
        }

        private static List<Item> ItemsOf(OpenXmlElement parent)
        {
            var items = new List<Item>();
            foreach (var child in parent.ChildElements)
            {
                switch (child)
                {
                    case W.ParagraphProperties:
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

                    case W.Hyperlink or W.InsertedRun or W.MoveToRun or W.CustomXmlRun or W.BidirectionalOverride or W.BidirectionalEmbedding:
                        items.Add(new ContainerItem(child, ItemsOf(child)));
                        break;

                    case W.SdtRun { SdtContentRun: { } content } contentControl when IsEditable(contentControl):
                        items.Add(new ContainerItem(child, ItemsOf(content)));
                        break;

                    // Fields without a result of their own, formulas, deleted runs, and whatever else:
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