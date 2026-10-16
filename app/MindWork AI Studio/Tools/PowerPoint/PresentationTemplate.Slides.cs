using System.Text;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal sealed partial class PresentationTemplate
{
    /// <summary>
    /// The titles an imprint goes by.
    /// </summary>
    private static readonly string[] IMPRINT_TITLES = [ImprintTexts.GERMAN.Title, "Imprint"];

    /// <summary>
    /// The beginnings of the lines of an imprint, e.g., "Thema" for the line naming the topic.
    /// </summary>
    /// <remarks>
    /// The labels lose their colon, so that "Autor" also finds "Autoren:".
    /// </remarks>
    private static readonly string[] IMPRINT_LABEL_STEMS = new[]
    {
        ImprintTexts.GERMAN.TopicLabel,
        ImprintTexts.GERMAN.DateLabel,
        ImprintTexts.GERMAN.AuthorLabel,
        ImprintTexts.GERMAN.InstituteLabel,
        ImprintTexts.GERMAN.PictureCreditsLabel,
    }.Select(label => label.TrimEnd(':')).ToArray();

    /// <summary>
    /// A slide with lines starting with at least this many labels of an imprint is one, whatever its title.
    /// </summary>
    /// <remarks>
    /// Three out of five: an author may well name the pictures "Bildquellen" rather than
    /// "Bildcredits", but hardly changes every line.
    /// </remarks>
    private const int MIN_IMPRINT_LABELS = 3;

    /// <summary>
    /// Reads the slides of a template with what their slots hold.
    /// </summary>
    /// <param name="presentationPart">The presentation part, which holds the slides.</param>
    /// <param name="presentation">The presentation, which lists the slides in their order.</param>
    /// <param name="layouts">Every layout of the template, including those without room for content.</param>
    /// <param name="families">The layout families the model plans with.</param>
    /// <returns>The slides, numbered as PowerPoint counts them.</returns>
    private static List<TemplateSlide> ReadSlides(PresentationPart presentationPart, P.Presentation presentation, IReadOnlyList<TemplateLayout> layouts, IReadOnlyList<LayoutFamily> families)
    {
        var layoutsByUri = new Dictionary<Uri, TemplateLayout>();
        foreach (var layout in layouts)
            layoutsByUri.TryAdd(layout.PartUri, layout);

        var familyIdsByLayoutUri = new Dictionary<Uri, string>();
        foreach (var family in families)
            foreach (var variant in family.Variants)
                familyIdsByLayoutUri.TryAdd(variant.Layout.PartUri, family.Id);

        var slides = new List<TemplateSlide>();
        foreach (var slideId in presentation.SlideIdList?.Elements<P.SlideId>() ?? [])
        {
            if (slideId.RelationshipId?.Value is not { } relationshipId || !presentationPart.TryGetPartById(relationshipId, out var slideCandidate) || slideCandidate is not SlidePart { Slide: { } slide } slidePart)
                continue;

            var layoutUri = slidePart.SlideLayoutPart?.Uri;
            var contents = layoutUri is not null && layoutsByUri.TryGetValue(layoutUri, out var slideLayout) ? ContentsOf(slide, slideLayout) : [];
            var title = contents.FirstOrDefault(content => content.Slot.Role is PlaceholderRole.TITLE)?.Text ?? string.Empty;
            slides.Add(new TemplateSlide(
                slides.Count + 1,
                slidePart.Uri,
                layoutUri,
                layoutUri is null ? null : familyIdsByLayoutUri.GetValueOrDefault(layoutUri),
                title.ReplaceLineEndings(" "),
                contents,
                slide.Show?.Value == false,
                IsImprint(title, contents)));
        }

        return slides;
    }

    /// <summary>
    /// What the slots of a slide hold, in the reading order of its layout.
    /// </summary>
    /// <remarks>
    /// Placeholders which fill no slot, such as the footer, and slots left empty are left out. A
    /// picture counts as content even though it has no text.
    /// </remarks>
    private static List<TemplateSlideContent> ContentsOf(P.Slide slide, TemplateLayout layout)
    {
        var textsBySlot = new Dictionary<TemplateSlot, string>();
        foreach (var placeholder in PlaceholdersOf(slide.CommonSlideData))
        {
            if (layout.SlotOf(placeholder.Type, placeholder.Index) is not { } slot)
                continue;

            var text = TextOf(placeholder.Element);
            if (text.Length > 0 || placeholder.Element is P.Picture)
                textsBySlot.TryAdd(slot, text);
        }

        return layout.Slots.Where(textsBySlot.ContainsKey).Select(slot => new TemplateSlideContent(slot, textsBySlot[slot])).ToList();
    }

    /// <summary>
    /// The text of a placeholder: the paragraphs of a shape, or the rows of a table.
    /// </summary>
    private static string TextOf(OpenXmlElement placeholder) => placeholder switch
    {
        P.Shape shape => TextOfBody(shape.TextBody),
        P.GraphicFrame frame => string.Join('\n', frame.Descendants<A.TableRow>().Select(row => string.Join(" | ", row.Elements<A.TableCell>().Select(cell => TextOfBody(cell.TextBody).ReplaceLineEndings(" "))))).Trim(),

        _ => string.Empty,
    };

    /// <summary>
    /// The text of a text body, one paragraph per line.
    /// </summary>
    /// <param name="textBody">The text body of a shape or of a table cell, which differ in type only.</param>
    private static string TextOfBody(OpenXmlElement? textBody) => string.Join('\n', (textBody?.Elements<A.Paragraph>() ?? []).Select(TextOfParagraph)).Trim();

    private static string TextOfParagraph(A.Paragraph paragraph)
    {
        var text = new StringBuilder();
        foreach (var element in paragraph.Elements())
        {
            switch (element)
            {
                case A.Run run:
                    text.Append(run.Text?.Text);
                    break;

                // A field, e.g., a date, shows the text it last had:
                case A.Field field:
                    text.Append(field.Text?.Text);
                    break;

                case A.Break:
                    text.Append('\n');
                    break;
            }
        }

        return text.ToString();
    }

    /// <summary>
    /// Says whether a slide is an imprint: by its title, or by the lines an imprint has.
    /// </summary>
    private static bool IsImprint(string title, IReadOnlyList<TemplateSlideContent> contents)
    {
        if (IMPRINT_TITLES.Any(imprintTitle => string.Equals(imprintTitle, title.Trim(), StringComparison.OrdinalIgnoreCase)))
            return true;

        var lines = contents.SelectMany(content => content.Text.Split('\n')).Select(line => line.TrimStart()).ToList();
        return IMPRINT_LABEL_STEMS.Count(stem => lines.Any(line => line.StartsWith(stem, StringComparison.OrdinalIgnoreCase))) >= MIN_IMPRINT_LABELS;
    }
}