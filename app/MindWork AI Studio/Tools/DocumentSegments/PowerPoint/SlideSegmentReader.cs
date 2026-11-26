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
    private sealed class ParagraphReader
    {
        private readonly TaggedTextBuilder builder = new();
        private readonly FormatSpanReader<A.RunProperties> spans;
        private readonly Dictionary<int, A.RunProperties?> formatPairs = [];
        private readonly Dictionary<int, OpenXmlElement> placeholders = [];

        public ParagraphReader(bool usesFormatPairs) => this.spans = new(this.builder, usesFormatPairs, FormatKeyOf);

        /// <summary>
        /// How many changes of formatting became pairs.
        /// </summary>
        public int FormatPairCount => this.spans.FormatPairCount;

        public SlideTextParagraph? TryRead(A.Paragraph paragraph)
        {
            var children = paragraph.ChildElements.Where(child => child is not (A.ParagraphProperties or A.EndParagraphRunProperties)).ToList();
            var baseFormatKey = this.spans.BaseKeyOf(children.OfType<A.Run>().Select(run => (TextOf(run), run.RunProperties)));

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
            if (this.spans.HasTagLikeWords)
                return null;

            return new(paragraph, new DocumentSegment(this.builder.Build()), this.formatPairs, this.placeholders, this.spans.BaseFormats.GetValueOrDefault(0), this.spans.HasMixedFormatting);
        }

        private void AddWords(List<A.Run> runs, string baseFormatKey) => this.spans.AddWords(runs.Select(run => (TextOf(run), run.RunProperties)), 0, baseFormatKey, (formatPair, format) => this.formatPairs[formatPair] = format);

        /// <summary>
        /// Tells the formatting of runs apart, leaving out what changes nothing about how words look, e.g.,
        /// their language or whether PowerPoint checked their spelling.
        /// </summary>
        private static string FormatKeyOf(A.RunProperties format)
        {
            var copy = (A.RunProperties)format.CloneNode(true);
            copy.Language = null;
            copy.AlternativeLanguage = null;
            copy.SpellingError = null;
            copy.Dirty = null;
            copy.NoProof = null;
            copy.SmartTagClean = null;

            // A run without anything that changes its look counts as having no properties at all:
            return copy.HasAttributes || copy.HasChildren ? copy.OuterXml : string.Empty;
        }

        private static string TextOf(A.Run run) => run.Text?.Text ?? string.Empty;
    }
}