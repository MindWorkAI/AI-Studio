using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.DocumentSegments.PowerPoint;

/// <summary>
/// Reads the paragraphs of a presentation as segments: those of the shapes, groups, and tables of each slide,
/// and those of its notes, slide by slide in the order of the presentation.
/// </summary>
/// <remarks>
/// <para>
/// The paragraphs of PowerPoint hold runs, line breaks, and fields side by side, without anything that encloses
/// runs. A link is part of the properties of its run, so it is a change of formatting like bold: such a change
/// becomes a pair of tags, see FormatSpans. Line breaks, fields such as the number of the slide, and everything
/// else become tags of their own.
/// </para>
/// <para>
/// The masters and layouts of the slides stay as they are: their texts are prompts for the authors, such as
/// "Click to add title", which a slide replaces with its own. Charts and SmartArt keep their texts in parts of
/// their own, which this reader leaves alone as well.
/// </para>
/// </remarks>
internal static class SlideSegmentReader
{
    /// <summary>
    /// Reads the paragraphs which can be segments, in the order of the presentation.
    /// </summary>
    /// <param name="document">The presentation.</param>
    public static IReadOnlyList<SlideTextParagraph> Read(PresentationDocument document)
    {
        var paragraphs = new List<SlideTextParagraph>();
        foreach (var root in RootsOf(document))
        {
            foreach (var paragraph in root.Descendants<A.Paragraph>())
            {
                if (ReadParagraph(paragraph) is { } read)
                    paragraphs.Add(read);
            }
        }

        return paragraphs;
    }

    private static IEnumerable<OpenXmlElement> RootsOf(PresentationDocument document)
    {
        if (document.PresentationPart is not { Presentation.SlideIdList: { } slideIds } presentationPart)
            yield break;

        foreach (var slideId in slideIds.Elements<P.SlideId>())
        {
            if (slideId.RelationshipId?.Value is not { } relationshipId || !presentationPart.TryGetPartById(relationshipId, out var part) || part is not SlidePart slidePart)
                continue;

            if (slidePart.Slide is { } slide)
                yield return slide;

            if (slidePart.NotesSlidePart?.NotesSlide is { } notes)
                yield return notes;
        }
    }

    /// <summary>
    /// Reads a paragraph, with its changes of formatting as pairs as long as they are few, see FormatSpans.MAX_PAIRS.
    /// </summary>
    private static SlideTextParagraph? ReadParagraph(A.Paragraph paragraph)
    {
        var reader = new ParagraphReader(usesFormatPairs: true);
        var read = reader.TryRead(paragraph);
        return reader.FormatPairCount <= FormatSpans.MAX_PAIRS ? read : new ParagraphReader(usesFormatPairs: false).TryRead(paragraph);
    }

    /// <summary>
    /// Reads one paragraph.
    /// </summary>
    private sealed class ParagraphReader(bool usesFormatPairs)
    {
        private readonly TaggedTextBuilder builder = new();
        private readonly Dictionary<int, A.RunProperties?> formatPairs = [];
        private readonly Dictionary<int, OpenXmlElement> placeholders = [];
        private readonly Dictionary<A.RunProperties, string> formatKeys = new(ReferenceEqualityComparer.Instance);
        private A.RunProperties? baseFormat;
        private string? noteFormatKey;
        private bool hasTagLikeWords;
        private bool hasMixedFormatting;

        /// <summary>
        /// How many changes of formatting became pairs.
        /// </summary>
        public int FormatPairCount { get; private set; }

        public SlideTextParagraph? TryRead(A.Paragraph paragraph)
        {
            var children = paragraph.ChildElements.Where(child => child is not (A.ParagraphProperties or A.EndParagraphRunProperties)).ToList();
            var baseFormatKey = FormatSpans.BaseKeyOf(children.OfType<A.Run>().Select(run => (TextOf(run), this.FormatKeyOf(run.RunProperties))));

            var words = new List<A.Run>();
            foreach (var child in children)
            {
                if (child is A.Run run)
                {
                    words.Add(run);
                    continue;
                }

                this.AddWords(words, baseFormatKey);
                words.Clear();
                this.placeholders[this.builder.Placeholder()] = child;
            }

            this.AddWords(words, baseFormatKey);

            // Words which read as a tag could not be told apart from the tags of the paragraph:
            if (this.hasTagLikeWords)
                return null;

            return new(paragraph, new DocumentSegment(this.builder.Build()), this.formatPairs, this.placeholders, this.baseFormat, this.hasMixedFormatting);
        }

        private void AddWords(List<A.Run> runs, string baseFormatKey)
        {
            foreach (var span in FormatSpans.Of(runs.Select(run => (TextOf(run), run.RunProperties, this.FormatKeyOf(run.RunProperties))), baseFormatKey))
            {
                this.hasTagLikeWords |= TaggedText.ContainsTagLikeText(span.Text);
                if (!usesFormatPairs || span.FormatKey == baseFormatKey)
                {
                    this.builder.Words(span.Text);
                    this.NoteFormat(span);
                    continue;
                }

                var formatPair = this.builder.Open();
                this.formatPairs[formatPair] = span.Format;
                this.builder.Words(span.Text);
                this.builder.Close(formatPair);
                this.FormatPairCount++;
            }
        }

        /// <summary>
        /// Notes the formatting of words outside of a pair, and whether it differs from earlier ones.
        /// </summary>
        private void NoteFormat(FormatSpan<A.RunProperties?> span)
        {
            if (this.noteFormatKey is null)
            {
                this.noteFormatKey = span.FormatKey;
                this.baseFormat = span.Format;
            }
            else if (this.noteFormatKey != span.FormatKey && !string.IsNullOrWhiteSpace(span.Text))
                this.hasMixedFormatting = true;
        }

        /// <summary>
        /// Tells the formatting of runs apart, leaving out what changes nothing about how words look, e.g.,
        /// their language or whether PowerPoint checked their spelling.
        /// </summary>
        private string FormatKeyOf(A.RunProperties? format)
        {
            if (format is null)
                return string.Empty;

            if (this.formatKeys.TryGetValue(format, out var formatKey))
                return formatKey;

            var copy = (A.RunProperties)format.CloneNode(true);
            copy.Language = null;
            copy.AlternativeLanguage = null;
            copy.SpellingError = null;
            copy.Dirty = null;
            copy.NoProof = null;
            copy.SmartTagClean = null;
            formatKey = copy.OuterXml;

            // A run without anything that changes its look counts as having no properties at all:
            if (!copy.HasAttributes && !copy.HasChildren)
                formatKey = string.Empty;

            this.formatKeys[format] = formatKey;
            return formatKey;
        }

        private static string TextOf(A.Run run) => run.Text?.Text ?? string.Empty;
    }
}