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
    private static readonly string[] IMPRINT_TITLES = ["Impressum", "Imprint"];

    /// <summary>
    /// The beginnings of the lines of an imprint, as the corporate template states them, e.g., "Thema" for the line naming the topic.
    /// </summary>
    /// <remarks>
    /// The labels lack their colon, so that "Autor" also finds "Autoren:".
    /// </remarks>
    private static readonly string[] IMPRINT_LABEL_STEMS = ["Thema", "Datum", "Autor", "Institut", "Bildcredits"];

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
            var topLevel = slide.CommonSlideData?.ShapeTree?.Elements().ToList() ?? [];
            var idCounts = topLevel.Select(TemplateSlideShapes.ShapeIdOf).OfType<uint>().CountBy(id => id).ToDictionary();
            var slotShapes = new HashSet<OpenXmlElement>();
            List<TemplateSlideContent> contents = [];
            List<TemplateSlot> movedSlots = [];
            List<TemplateSlot> slots = [];
            List<TemplateSlotCopy> slotCopies = [];
            if (layoutUri is not null && layoutsByUri.TryGetValue(layoutUri, out var slideLayout))
            {
                var shapesBySlot = TemplateSlideShapes.SlotShapesOf(slide.CommonSlideData?.ShapeTree, slideLayout);
                movedSlots = MovedSlotsOf(shapesBySlot, slideLayout, slideWidth, slideHeight);
                var slotsOnSlide = slideLayout.Slots.Select(slot => movedSlots.FirstOrDefault(moved => moved.Role == slot.Role && moved.Ordinal == slot.Ordinal) ?? slot).ToList();
                var copies = SlotCopiesOf(topLevel, idCounts, shapesBySlot, slideLayout, slotsOnSlide, slideWidth, slideHeight);
                slotCopies = copies.Select(found => found.Copy).ToList();
                slots = InReadingOrderWith(slotsOnSlide, slotCopies.Select(copy => copy.Slot), slideHeight);

                // What fills a slot, by the name the model knows it by:
                var filledSlots = shapesBySlot.ToDictionary(pair => (pair.Key.Role, pair.Key.Ordinal), pair => (Slot: pair.Key, Shape: pair.Value));
                foreach (var (copy, shape) in copies)
                    filledSlots[(copy.Slot.Role, copy.Slot.Ordinal)] = (copy.Slot, shape);

                contents = ContentsOf(slots, filledSlots);
                slotShapes = filledSlots.Values.Select(filled => filled.Shape).ToHashSet();
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
                slots,
                slotCopies,
                ElementsOf(topLevel, idCounts, slotShapes),
                slide.Show?.Value == false,
                IsImprint(title, contents)));
        }

        return slides;
    }

    /// <summary>
    /// What the slots of a slide hold, in reading order.
    /// </summary>
    /// <remarks>
    /// Placeholders which fill no slot, such as the footer, and slots left empty are left out. A
    /// picture counts as content even though it has no text, and a table which is no placeholder
    /// as the content of the table slot, see <see cref="TemplateSlideShapes.SlotShapesOf"/>.
    /// </remarks>
    /// <param name="slots">The slots the slide offers, in reading order.</param>
    /// <param name="filledSlots">The shapes filling the slots, by role and number: the slot of the layout, or the copy, and its shape.</param>
    private static List<TemplateSlideContent> ContentsOf(IReadOnlyList<TemplateSlot> slots, IReadOnlyDictionary<(PlaceholderRole Role, int Ordinal), (TemplateSlot Slot, OpenXmlElement Shape)> filledSlots)
    {
        var contents = new List<TemplateSlideContent>();
        foreach (var slot in slots)
        {
            if (!filledSlots.TryGetValue((slot.Role, slot.Ordinal), out var filled))
                continue;

            var text = TextOf(filled.Shape);
            if (text.Length > 0 || filled.Shape is P.Picture)
                contents.Add(new TemplateSlideContent(filled.Slot, text));
        }

        return contents;
    }

    /// <summary>
    /// Finds the shapes of a slide which repeat a text or a label slot of its layout, e.g., a second
    /// heading with its text, which the author copied from the placeholders of the slide.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PowerPoint turns a copied placeholder into a text box which keeps the look of the placeholder,
    /// so what tells a copy is its geometry: it has the height of the slot it repeats, a text also
    /// its width, and it lines up with the slot or with another copy of it, below it in a column or
    /// beside it in a row. A label may be narrower or wider, since authors fit a heading to its text.
    /// The name of the shape would tell as well, but it depends on the language of PowerPoint, and
    /// other programs do not keep it.
    /// </para>
    /// <para>
    /// A copy overlaps no slot and no other copy: a text box lying on a slot is in the way of it, so it
    /// stays an element, which the model may remove. A second placeholder of a slot whose first one is
    /// taken may be a copy as well. Size is compared with the slot itself, so that a long row of
    /// copies does not drift by a tolerance per step.
    /// </para>
    /// </remarks>
    /// <param name="topLevel">The top-level elements of the slide.</param>
    /// <param name="idCounts">How often each shape ID occurs among them; a copy is found again by its ID, which therefore has to be unique.</param>
    /// <param name="shapesBySlot">The shapes of the slide which fill the slots of its layout.</param>
    /// <param name="layout">The layout of the slide.</param>
    /// <param name="slotsOnSlide">The slots of the layout, where the slide puts them; a slot the slide leaves out at the place of the layout.</param>
    /// <param name="slideWidth">The width of a slide, in EMU.</param>
    /// <param name="slideHeight">The height of a slide, in EMU.</param>
    /// <returns>The copies with their shapes, in reading order, numbered after the slots of the layout.</returns>
    private static List<(TemplateSlotCopy Copy, P.Shape Shape)> SlotCopiesOf(IReadOnlyList<OpenXmlElement> topLevel, IReadOnlyDictionary<uint, int> idCounts, IReadOnlyDictionary<TemplateSlot, OpenXmlElement> shapesBySlot, TemplateLayout layout, IReadOnlyList<TemplateSlot> slotsOnSlide, long slideWidth, long slideHeight)
    {
        var horizontalTolerance = (long)(slideWidth * SLOT_TOLERANCE);
        var verticalTolerance = (long)(slideHeight * SLOT_TOLERANCE);

        // What a copy may repeat: a text or a label the slide carries, and every copy found so far:
        var references = slotsOnSlide
            .Where(slot => slot.Role is (PlaceholderRole.TEXT or PlaceholderRole.LABEL) && shapesBySlot.Keys.Any(filled => filled.Role == slot.Role && filled.Ordinal == slot.Ordinal))
            .Select(slot => new CopyReference(slot.Bounds, slot))
            .ToList();

        if (references.Count == 0)
            return [];

        var slotShapes = shapesBySlot.Values.ToHashSet();
        var candidates = new List<(P.Shape Shape, uint Id, SlotBounds Bounds)>();
        foreach (var element in topLevel)
        {
            if (element is not P.Shape shape || slotShapes.Contains(shape) || TemplateSlideShapes.ShapeIdOf(shape) is not { } id || idCounts[id] > 1 || TextOf(shape).Length == 0)
                continue;

            if (TemplateSlideShapes.PlaceholderOf(shape) is { } placeholder && layout.SlotOf(placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value)?.Role is not (PlaceholderRole.TEXT or PlaceholderRole.LABEL))
                continue;

            if (BoundsOf(shape.ShapeProperties?.Transform2D?.Offset, shape.ShapeProperties?.Transform2D?.Extents) is { } bounds)
                candidates.Add((shape, id, bounds));
        }

        var occupied = slotsOnSlide.Select(slot => slot.Bounds).ToList();
        var remaining = candidates.OrderBy(candidate => ReadingKeyOf(candidate.Bounds, slideHeight)).ToList();
        var found = new List<(P.Shape Shape, uint Id, SlotBounds Bounds, TemplateSlot Original)>();
        bool foundMore;
        do
        {
            foundMore = false;
            foreach (var candidate in remaining.ToList())
            {
                if (occupied.Any(bounds => Overlaps(candidate.Bounds, bounds)))
                {
                    remaining.Remove(candidate);
                    continue;
                }

                var reference = references
                    .Where(other => Repeats(candidate.Bounds, other, horizontalTolerance, verticalTolerance))
                    .MinBy(other => Math.Abs(candidate.Bounds.X - other.Bounds.X) + Math.Abs(candidate.Bounds.Y - other.Bounds.Y));

                if (reference is null)
                    continue;

                found.Add((candidate.Shape, candidate.Id, candidate.Bounds, reference.Original));
                references.Add(new CopyReference(candidate.Bounds, reference.Original));
                occupied.Add(candidate.Bounds);
                remaining.Remove(candidate);
                foundMore = true;
            }
        } while (foundMore);

        var counters = layout.Slots.CountBy(slot => slot.Role).ToDictionary();
        var copies = new List<(TemplateSlotCopy Copy, P.Shape Shape)>();
        foreach (var copy in found.OrderBy(candidate => ReadingKeyOf(candidate.Bounds, slideHeight)))
        {
            var ordinal = counters.GetValueOrDefault(copy.Original.Role) + 1;
            counters[copy.Original.Role] = ordinal;
            copies.Add((new TemplateSlotCopy(copy.Original.MovedTo(copy.Bounds) with { Ordinal = ordinal }, copy.Id), copy.Shape));
        }

        return copies;
    }

    /// <summary>
    /// Whether a shape at the given place repeats the slot of a reference: with the size of the slot,
    /// in line with the reference.
    /// </summary>
    private static bool Repeats(SlotBounds bounds, CopyReference reference, long horizontalTolerance, long verticalTolerance)
    {
        var original = reference.Original.Bounds;
        var hasItsSize = Math.Abs(bounds.Height - original.Height) <= verticalTolerance && (reference.Original.Role is PlaceholderRole.LABEL || Math.Abs(bounds.Width - original.Width) <= horizontalTolerance);
        var isInLine = Math.Abs(bounds.X - reference.Bounds.X) <= horizontalTolerance || Math.Abs(bounds.Y - reference.Bounds.Y) <= verticalTolerance;
        return hasItsSize && isInLine;
    }

    /// <summary>
    /// Something a copy of a slot may line up with: the slot itself, or a copy of it found before.
    /// </summary>
    /// <param name="Bounds">Where the slot or the copy sits on the slide.</param>
    /// <param name="Original">The slot of the layout which is repeated, where the slide puts it.</param>
    private sealed record CopyReference(SlotBounds Bounds, TemplateSlot Original);

    /// <summary>
    /// Adds the copies of slots to the slots of a slide, each one where a reader comes across it.
    /// </summary>
    /// <remarks>
    /// The slots of the layout keep their order, so that a slide without copies offers its slots
    /// exactly as the layout does. A copy goes after the last slot a reader takes in before it.
    /// </remarks>
    /// <param name="slots">The slots of the layout, where the slide puts them.</param>
    /// <param name="copies">The slots the copies offer, in reading order.</param>
    /// <param name="slideHeight">The height of a slide, in EMU.</param>
    private static List<TemplateSlot> InReadingOrderWith(IReadOnlyList<TemplateSlot> slots, IEnumerable<TemplateSlot> copies, long slideHeight)
    {
        var merged = slots.ToList();
        foreach (var copy in copies)
        {
            var key = ReadingKeyOf(copy.Bounds, slideHeight);
            var position = merged.FindLastIndex(slot => ReadingKeyOf(slot.Bounds, slideHeight).CompareTo(key) <= 0);
            merged.Insert(position + 1, copy);
        }

        return merged;
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
    /// What a slide carries besides the shapes filling the slots of its layout and their copies, from back to front.
    /// </summary>
    /// <remarks>
    /// Only what shows counts: footer, slide number and date belong to the presentation, a
    /// placeholder without text shows nothing, and neither does a shape without text, fill or line.
    /// The writer finds an element again by its ID, so an element whose ID another one shares on the
    /// slide is left out rather than risking the wrong one. PowerPoint itself never writes such IDs.
    /// </remarks>
    /// <param name="topLevel">The top-level elements of the slide.</param>
    /// <param name="idCounts">How often each shape ID occurs among them.</param>
    /// <param name="slotShapes">The shapes filling the slots of its layout, and the copies of slots.</param>
    private static List<TemplateSlideElement> ElementsOf(IReadOnlyList<OpenXmlElement> topLevel, IReadOnlyDictionary<uint, int> idCounts, IReadOnlySet<OpenXmlElement> slotShapes)
    {
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