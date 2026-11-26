using System.Globalization;
using System.Text;

using AIStudio.Tools.OpenXml;

using DocumentFormat.OpenXml;

using W = DocumentFormat.OpenXml.Wordprocessing;

namespace AIStudio.Tools.DocumentSegments.Word;

/// <summary>
/// Puts a paragraph of a Word document together again around its new words.
/// </summary>
/// <remarks>
/// The properties of the paragraph stay, and so do all elements its tags stand for: the writer moves them into
/// the new paragraph rather than copying them, so that a text box keeps the new words of its own paragraphs,
/// whichever paragraph is written first. New words get the formatting of their innermost pair.
/// </remarks>
internal static class WordParagraphWriter
{
    private const string WORDPROCESSING_NAMESPACE = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// Replaces the content of a paragraph with its new words.
    /// </summary>
    /// <param name="paragraph">The paragraph, as the reader read it from this document.</param>
    /// <param name="newText">The new words with tags, as the model wrote them.</param>
    /// <param name="languageTag">The IETF tag of the language of the new words; empty to leave the languages as they are.</param>
    /// <returns>Whether the paragraph kept all of its inline structure and formatting.</returns>
    public static bool Write(WordParagraph paragraph, string newText, string languageTag)
    {
        var element = paragraph.Paragraph;
        foreach (var child in element.ChildElements.Where(child => child is not W.ParagraphProperties).ToList())
            child.Remove();

        foreach (var marker in paragraph.StartMarkers)
            element.AppendChild(marker.Detached());

        var writtenAtoms = new HashSet<WordAtom>();
        bool keepsTags;
        if (TaggedText.TryRead(newText, out var taggedText, out _))
        {
            var tokenWriter = new TokenWriter(paragraph, languageTag, writtenAtoms);
            tokenWriter.Write(taggedText);
            keepsTags = taggedText.HasSameTagsAs(paragraph.Segment.Source, out _) && tokenWriter.KeptAllContainers;
        }
        else
        {
            keepsTags = false;
            AppendWords(element, TaggedText.RemoveTags(newText), paragraph.TextFormatOf(0), languageTag);
        }

        // What the model dropped follows its words, so that no picture or reference gets lost:
        foreach (var atom in paragraph.AtomsInOrder.Where(atom => !writtenAtoms.Contains(atom)))
            AppendAtom(element, atom);

        foreach (var marker in paragraph.EndMarkers)
            element.AppendChild(marker.Detached());

        return keepsTags && !paragraph.HasMixedFormatting;
    }

    /// <summary>
    /// Whether Word allows an element within another one.
    /// </summary>
    /// <remarks>
    /// The writer builds the pairs as the model nested them and puts the tags of their own where the model put them,
    /// which may be a place the source never had. Within an insertion or a move of the change tracking, Word allows
    /// runs and what lies between runs, but no link, simple field, custom XML, or subdocument; it would offer to
    /// repair the file. Everything else the writer creates may stand where the model put it.
    /// </remarks>
    /// <param name="parent">Where the element would go.</param>
    /// <param name="child">The element.</param>
    private static bool Allows(OpenXmlElement parent, OpenXmlElement child) =>
        parent is not (W.InsertedRun or W.MoveToRun) || child is not (W.Hyperlink or W.SimpleField or W.CustomXmlRun or W.SubDocumentReference);

    /// <summary>
    /// Writes the tokens of the new words into the paragraph, one after the other.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only pairs whose both tags arrived go back; TaggedText.TryRead made sure that they nest. When the tags do not
    /// fit the source, the pairs follow the nesting of the model, so that a link keeps its words although the model
    /// dropped a tab. The writer keeps that valid for Word, see Allows:
    /// </para>
    /// <list type="bullet">
    /// <item>A tag of its own, e.g., a linked logo which the model moved into an insertion, goes right after the
    /// insertion, and the insertion goes on behind it. Nothing gets lost, so this counts as kept.</item>
    /// <item>A pair whose container may not stand within its outer pair, e.g., a link which the model put into an
    /// insertion, loses its container and keeps its words. This happens only when the tags do not fit anyway.</item>
    /// </list>
    /// </remarks>
    private sealed class TokenWriter(WordParagraph paragraph, string languageTag, HashSet<WordAtom> writtenAtoms)
    {
        /// <summary>
        /// A pair whose opening tag was written and whose closing tag is still to come.
        /// </summary>
        /// <param name="id">The number of the pair.</param>
        /// <param name="outer">Where the content goes again once the pair closes, i.e., where its container stands.</param>
        /// <param name="container">The container the pair stands for; null when it has none, or when it lost it.</param>
        private sealed class OpenPair(int id, OpenXmlElement outer, OpenXmlElement? container)
        {
            public int Id => id;

            /// <summary>
            /// Where the container stands; it moves when the container is opened again behind an element, see AppendWhereAllowed.
            /// </summary>
            public OpenXmlElement Outer { get; set; } = outer;

            public OpenXmlElement? Container => container;

            /// <summary>
            /// The copy of the container which was opened again behind an element which may not stand within it, and the
            /// element within the copy which holds the words; null as long as the first copy takes the words.
            /// </summary>
            public (OpenXmlElement Shell, OpenXmlElement Content)? Reopened { get; set; }
        }

        private readonly List<OpenPair> openPairs = [];
        private OpenXmlElement target = paragraph.Paragraph;

        /// <summary>
        /// Whether every pair whose both tags arrived kept its container.
        /// </summary>
        public bool KeptAllContainers { get; private set; } = true;

        public void Write(TaggedText taggedText)
        {
            var openIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.OPEN).Select(token => token.Id).ToHashSet();
            var closeIds = taggedText.Tokens.Where(token => token.Kind is TaggedTokenKind.CLOSE).Select(token => token.Id).ToHashSet();
            foreach (var token in taggedText.Tokens)
            {
                switch (token.Kind)
                {
                    case TaggedTokenKind.TEXT:
                        AppendWords(this.target, token.Text, paragraph.TextFormatOf(this.openPairs.Count == 0 ? 0 : this.openPairs[^1].Id), languageTag);
                        break;

                    case TaggedTokenKind.OPEN when closeIds.Contains(token.Id) && paragraph.Pairs.TryGetValue(token.Id, out var openedPair):
                        this.Open(token.Id, openedPair);
                        break;

                    case TaggedTokenKind.CLOSE when openIds.Contains(token.Id) && paragraph.Pairs.TryGetValue(token.Id, out var closedPair):
                        this.Close();
                        if (closedPair is WordFieldPair { Closing: var closing })
                            this.AppendAtom(closing);

                        break;

                    case TaggedTokenKind.PLACEHOLDER when paragraph.Placeholders.TryGetValue(token.Id, out var atom) && !writtenAtoms.Contains(atom):
                        this.AppendAtom(atom);
                        break;
                }
            }
        }

        private void Open(int pairId, WordPair pair)
        {
            switch (pair)
            {
                case WordContainerPair { Container: var container } when Allows(this.target, container):
                    var (shell, content) = ShellOf(container);
                    this.target.AppendChild(shell);
                    this.openPairs.Add(new(pairId, this.target, container));
                    this.target = content;
                    break;

                // The words stay where they are, without the container:
                case WordContainerPair:
                    this.KeptAllContainers = false;
                    this.openPairs.Add(new(pairId, this.target, null));
                    break;

                case WordFieldPair field:
                    this.openPairs.Add(new(pairId, this.target, null));
                    this.AppendAtom(field.Opening);
                    break;

                // The words within the pair take its formatting, see WordParagraph.TextFormatOf:
                default:
                    this.openPairs.Add(new(pairId, this.target, null));
                    break;
            }
        }

        private void Close()
        {
            var openPair = this.openPairs[^1];
            this.openPairs.RemoveAt(this.openPairs.Count - 1);
            this.target = openPair.Outer;

            // The rest of an insertion behind a tag which moved out of it would be empty ballast, when nothing follows the tag:
            if (openPair.Reopened is (var shell, var content) && !content.HasChildren)
                shell.Remove();
        }

        private void AppendAtom(WordAtom atom)
        {
            writtenAtoms.Add(atom);
            foreach (var piece in atom.Pieces)
            {
                var element = piece.Element.Detached();
                if (piece.InRun)
                    this.target.AppendChild(RunOf(piece, element));
                else
                    this.AppendWhereAllowed(element);
            }
        }

        /// <summary>
        /// Appends an element to the innermost container which allows it, and opens the containers it left again behind it.
        /// </summary>
        private void AppendWhereAllowed(OpenXmlElement element)
        {
            var level = this.openPairs.Count;
            var place = this.target;
            while (!Allows(place, element) && level > 0)
            {
                level--;
                place = this.openPairs[level].Outer;
            }

            place.AppendChild(element);
            for (; level < this.openPairs.Count; level++)
            {
                var openPair = this.openPairs[level];
                openPair.Outer = place;
                if (openPair.Container is null)
                    continue;

                var (shell, content) = ShellOf(openPair.Container);
                place.AppendChild(shell);

                // Word tells the changes of its change tracking apart by their numbers, so the second part of an insertion needs a number of its own:
                if (shell is W.RunTrackChangeType change)
                    change.Id = NextAnnotationId(shell);

                openPair.Reopened = (shell, content);
                place = content;
            }

            this.target = place;
        }
    }

    /// <summary>
    /// Creates an empty copy of an element which encloses runs, with its attributes and properties.
    /// </summary>
    /// <returns>The copy, and the element within it which takes the runs.</returns>
    private static (OpenXmlElement Shell, OpenXmlElement Content) ShellOf(OpenXmlElement container)
    {
        var shell = container.CloneNode(false);
        switch (container)
        {
            case W.SdtRun contentControl:
                if (contentControl.SdtProperties is { } contentControlProperties)
                    shell.AppendChild(contentControlProperties.CloneNode(true));

                if (contentControl.SdtEndCharProperties is { } endProperties)
                    shell.AppendChild(endProperties.CloneNode(true));

                return (shell, shell.AppendChild(new W.SdtContentRun()));

            case W.CustomXmlRun { CustomXmlProperties: { } customXmlProperties }:
                shell.AppendChild(customXmlProperties.CloneNode(true));
                break;

            case W.SimpleField { FieldData: { } fieldData }:
                shell.AppendChild(fieldData.CloneNode(true));
                break;
        }

        return (shell, shell);
    }

    private static void AppendAtom(OpenXmlElement target, WordAtom atom)
    {
        foreach (var piece in atom.Pieces)
        {
            var element = piece.Element.Detached();
            target.AppendChild(piece.InRun ? RunOf(piece, element) : element);
        }
    }

    /// <summary>
    /// Finds a number which no change, comment, or bookmark of the part uses yet.
    /// </summary>
    /// <param name="element">An element within the part, e.g., within the document or a header.</param>
    private static string NextAnnotationId(OpenXmlElement element)
    {
        var root = element.Ancestors().LastOrDefault() ?? element;
        var highest = root.Descendants()
            .SelectMany(descendant => descendant.GetAttributes())
            .Where(attribute => attribute is { LocalName: "id", NamespaceUri: WORDPROCESSING_NAMESPACE })
            .Select(attribute => int.TryParse(attribute.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .DefaultIfEmpty(0)
            .Max();

        return (highest + 1).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Puts an element which belongs into a run, e.g., a tab or a picture, into a run of its own with its formatting.
    /// </summary>
    private static W.Run RunOf(WordPiece piece, OpenXmlElement element)
    {
        var run = new W.Run();
        if (piece.RunProperties is { } format)
            run.AppendChild(format.CloneNode(true));

        run.AppendChild(element);
        return run;
    }

    /// <summary>
    /// Appends words as a run, with tabs and line breaks as Word writes them.
    /// </summary>
    private static void AppendWords(OpenXmlElement target, string words, W.RunProperties? format, string languageTag)
    {
        if (words.Length == 0)
            return;

        var run = new W.Run();
        if (format is not null)
            run.AppendChild((W.RunProperties)format.CloneNode(true));

        var text = new StringBuilder();
        foreach (var character in words)
        {
            switch (character)
            {
                case '\t':
                    AppendText(run, text);
                    run.AppendChild(new W.TabChar());
                    break;

                case '\n':
                    AppendText(run, text);
                    run.AppendChild(new W.Break());
                    break;

                case '\r':
                    break;

                default:
                    text.Append(character);
                    break;
            }
        }

        AppendText(run, text);
        WordLanguage.Set(run, languageTag);
        target.AppendChild(run);
    }

    private static void AppendText(W.Run run, StringBuilder text)
    {
        if (text.Length == 0)
            return;

        run.AppendChild(new W.Text(OpenXmlText.XmlSafe(text.ToString())) { Space = SpaceProcessingModeValues.Preserve });
        text.Clear();
    }
}