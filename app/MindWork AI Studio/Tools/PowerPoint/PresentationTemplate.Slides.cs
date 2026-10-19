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
    /// <param name="slideWidth">The width of a slide, in EMU.</param>
    /// <param name="slideHeight">The height of a slide, in EMU.</param>
    /// <returns>The slides, numbered as PowerPoint counts them.</returns>
    private static List<TemplateSlide> ReadSlides(PresentationPart presentationPart, P.Presentation presentation, IReadOnlyList<TemplateLayout> layouts, IReadOnlyList<LayoutFamily> families, long slideWidth, long slideHeight)
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
            var shapeTree = slide.CommonSlideData?.ShapeTree;
            var shapesBySlot = new Dictionary<TemplateSlot, OpenXmlElement>();
            List<TemplateSlideContent> contents = [];
            List<TemplateSlot> movedSlots = [];
            if (layoutUri is not null && layoutsByUri.TryGetValue(layoutUri, out var slideLayout))
            {
                shapesBySlot = TemplateSlideShapes.SlotShapesOf(shapeTree, slideLayout);
                contents = ContentsOf(shapesBySlot, slideLayout);
                movedSlots = MovedSlotsOf(shapesBySlot, slideLayout, slideWidth, slideHeight);
            }

            var title = contents.FirstOrDefault(content => content.Slot.Role is PlaceholderRole.TITLE)?.Text ?? string.Empty;
            slides.Add(new TemplateSlide(
                slides.Count + 1,
                slidePart.Uri,
                layoutUri,
                layoutUri is null ? null : familyIdsByLayoutUri.GetValueOrDefault(layoutUri),
                title.ReplaceLineEndings(" "),
                contents,
                movedSlots,
                ElementsOf(shapeTree, shapesBySlot),
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
    /// picture counts as content even though it has no text, and a table which is no placeholder
    /// as the content of the table slot, see <see cref="TemplateSlideShapes.SlotShapesOf"/>.
    /// </remarks>
    /// <param name="shapesBySlot">The shapes of the slide which fill the slots of its layout.</param>
    /// <param name="layout">The layout of the slide.</param>
    private static List<TemplateSlideContent> ContentsOf(IReadOnlyDictionary<TemplateSlot, OpenXmlElement> shapesBySlot, TemplateLayout layout)
    {
        var contents = new List<TemplateSlideContent>();
        foreach (var slot in layout.Slots)
        {
            if (!shapesBySlot.TryGetValue(slot, out var shape))
                continue;

            var text = TextOf(shape);
            if (text.Length > 0 || shape is P.Picture)
                contents.Add(new TemplateSlideContent(slot, text));
        }

        return contents;
    }

    /// <summary>
    /// The slots whose placeholders a slide moved or resized, with their place and capacity on the slide.
    /// </summary>
    /// <remarks>
    /// Authors shrink a placeholder to make room for something of their own, such as a picture
    /// beside the text. The model then has to write for the room the slide leaves, not for the one
    /// the layout offers. Small shifts, as they happen when dragging a placeholder, do not count.
    /// </remarks>
    /// <param name="shapesBySlot">The shapes of the slide which fill the slots of its layout.</param>
    /// <param name="layout">The layout of the slide.</param>
    /// <param name="slideWidth">The width of a slide, in EMU.</param>
    /// <param name="slideHeight">The height of a slide, in EMU.</param>
    private static List<TemplateSlot> MovedSlotsOf(IReadOnlyDictionary<TemplateSlot, OpenXmlElement> shapesBySlot, TemplateLayout layout, long slideWidth, long slideHeight)
    {
        var horizontalTolerance = (long)(slideWidth * SLOT_TOLERANCE);
        var verticalTolerance = (long)(slideHeight * SLOT_TOLERANCE);
        var movedSlots = new List<TemplateSlot>();
        foreach (var slot in layout.Slots)
        {
            if (shapesBySlot.TryGetValue(slot, out var shape) && BoundsOnSlideOf(slot, shape, horizontalTolerance, verticalTolerance) is { } bounds && !bounds.IsCloseTo(slot.Bounds, horizontalTolerance, verticalTolerance))
                movedSlots.Add(slot.MovedTo(bounds));
        }

        return movedSlots;
    }

    /// <summary>
    /// Where the shape filling a slot puts the slot on the slide.
    /// </summary>
    /// <returns>The place; null when the shape says nothing about it, e.g., because it takes the place of the layout.</returns>
    private static SlotBounds? BoundsOnSlideOf(TemplateSlot slot, OpenXmlElement shape, long horizontalTolerance, long verticalTolerance) => shape switch
    {
        P.Shape textShape => BoundsOf(textShape.ShapeProperties?.Transform2D?.Offset, textShape.ShapeProperties?.Transform2D?.Extents),

        // A table grows with its rows, so its frame tells where the slot starts, but not how far down it reaches:
        P.GraphicFrame frame => BoundsOf(frame.Transform?.Offset, frame.Transform?.Extents) is { } frameBounds
            ? frameBounds with { Height = Math.Max(frameBounds.Height, slot.Bounds.Bottom - frameBounds.Y) }
            : null,

        // A picture is fit into its slot, so one within the slot does not say that the slot moved:
        P.Picture picture => BoundsOf(picture.ShapeProperties?.Transform2D?.Offset, picture.ShapeProperties?.Transform2D?.Extents) is { } pictureBounds && !slot.Bounds.Contains(pictureBounds, horizontalTolerance, verticalTolerance)
            ? pictureBounds
            : null,

        _ => null,
    };

    /// <summary>
    /// What a slide carries besides the shapes filling the slots of its layout, from back to front.
    /// </summary>
    /// <remarks>
    /// Only what shows counts: footer, slide number and date belong to the presentation, a
    /// placeholder without text shows nothing, and neither does a shape without text, fill or line.
    /// The writer finds an element again by its ID, so an element whose ID another one shares on the
    /// slide is left out rather than risking the wrong one. PowerPoint itself never writes such IDs.
    /// </remarks>
    /// <param name="shapeTree">The shape tree of the slide.</param>
    /// <param name="shapesBySlot">The shapes filling the slots of its layout.</param>
    private static List<TemplateSlideElement> ElementsOf(P.ShapeTree? shapeTree, IReadOnlyDictionary<TemplateSlot, OpenXmlElement> shapesBySlot)
    {
        var slotShapes = shapesBySlot.Values.ToHashSet();
        var topLevel = shapeTree?.Elements().ToList() ?? [];
        var idCounts = topLevel.Select(TemplateSlideShapes.ShapeIdOf).OfType<uint>().CountBy(id => id).ToDictionary();
        var elements = new List<TemplateSlideElement>();
        foreach (var element in topLevel)
        {
            if (slotShapes.Contains(element) || TemplateSlideShapes.ShapeIdOf(element) is not { } id || idCounts[id] > 1 || KindOf(element) is not { } kind)
                continue;

            elements.Add(new TemplateSlideElement(elements.Count + 1, id, kind, ElementBoundsOf(element), ElementTextOf(element)));
        }

        return elements;
    }

    /// <summary>
    /// What kind of element a top-level element of a slide is.
    /// </summary>
    /// <returns>The kind; null when the element shows nothing worth naming, or belongs to the presentation as a whole.</returns>
    private static TemplateSlideElementKind? KindOf(OpenXmlElement element)
    {
        switch (element)
        {
            case P.Shape shape:
                var hasText = TextOf(shape).Length > 0;
                if (TemplateSlideShapes.PlaceholderOf(shape) is { } placeholder)
                    return hasText && placeholder.Type?.InnerText is not ("ftr" or "sldNum" or "dt" or "hdr") ? TemplateSlideElementKind.TEXT_BOX : null;

                return hasText ? TemplateSlideElementKind.TEXT_BOX : IsVisible(shape) ? TemplateSlideElementKind.SHAPE : null;

            case P.Picture:
                return TemplateSlideElementKind.PICTURE;

            case P.GraphicFrame frame:
                var uri = frame.Graphic?.GraphicData?.Uri?.Value ?? string.Empty;
                if (uri.EndsWith("/table", StringComparison.Ordinal))
                    return TemplateSlideElementKind.TABLE;

                if (uri.Contains("chart", StringComparison.OrdinalIgnoreCase))
                    return TemplateSlideElementKind.CHART;

                return uri.EndsWith("/diagram", StringComparison.Ordinal) ? TemplateSlideElementKind.DIAGRAM : TemplateSlideElementKind.OBJECT;

            case P.GroupShape:
                return TemplateSlideElementKind.GROUP;

            case P.ConnectionShape:
                return TemplateSlideElementKind.LINE;

            case AlternateContent:
                return TemplateSlideElementKind.OBJECT;

            default:
                return null;
        }
    }

    /// <summary>
    /// Whether a shape without text shows anything: a fill or a line, of its own or from its style.
    /// </summary>
    private static bool IsVisible(P.Shape shape)
    {
        var properties = shape.ShapeProperties;
        var hasFill = properties?.GetFirstChild<A.NoFill>() is null &&
                      (properties?.Elements().Any(child => child is A.SolidFill or A.GradientFill or A.BlipFill or A.PatternFill or A.GroupFill) is true || shape.ShapeStyle?.FillReference?.Index?.Value > 0);

        var hasLine = properties?.GetFirstChild<A.Outline>() is { } outline
            ? outline.GetFirstChild<A.NoFill>() is null
            : shape.ShapeStyle?.LineReference?.Index?.Value > 0;

        return hasFill || hasLine;
    }

    /// <summary>
    /// Where a top-level element of a slide sits.
    /// </summary>
    private static SlotBounds? ElementBoundsOf(OpenXmlElement element) => element switch
    {
        P.GraphicFrame frame => BoundsOf(frame.Transform?.Offset, frame.Transform?.Extents),
        P.GroupShape group => BoundsOf(group.GroupShapeProperties?.TransformGroup?.Offset, group.GroupShapeProperties?.TransformGroup?.Extents),

        _ => element.Descendants<A.Transform2D>().FirstOrDefault() is { } transform ? BoundsOf(transform.Offset, transform.Extents) : null,
    };

    /// <summary>
    /// The text of a top-level element of a slide: the rows of a table, or every paragraph it holds, those of a group included.
    /// </summary>
    /// <remarks>
    /// Something wrapped for newer versions of PowerPoint comes with a fallback for older ones, which
    /// would repeat the text; only the first choice counts.
    /// </remarks>
    private static string ElementTextOf(OpenXmlElement element) => element switch
    {
        P.GraphicFrame => TextOf(element),
        AlternateContent { FirstChild: { } choice } => ElementTextOf(choice),

        _ => string.Join('\n', element.Descendants<A.Paragraph>().Select(TextOfParagraph).Where(text => text.Trim().Length > 0)).Trim(),
    };

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