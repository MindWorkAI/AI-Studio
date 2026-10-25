using System.Globalization;

using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal static partial class PresentationWriter
{
    /// <summary>
    /// The namespace of the attributes by which the XML of a part refers to other parts.
    /// </summary>
    private const string RELATIONSHIPS_NAMESPACE = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    /// <summary>
    /// Where the template slides the deck refers to are stored inside the file.
    /// </summary>
    private static HashSet<Uri> TemplateSlidePartUrisOf(PresentationTemplate template, IEnumerable<SlideSpec> slides) => slides
        .Select(slide => slide.TemplateSlide is { } number ? template.FindSlide(number)?.PartUri : null)
        .OfType<Uri>()
        .ToHashSet();

    /// <summary>
    /// The kept slide part of a template slide, which the presentation lists once only.
    /// </summary>
    /// <param name="keptSlideParts">The slides the deck keeps, by where they are stored.</param>
    /// <param name="usedTemplateSlides">Where the template slides written so far are stored; the slide is added.</param>
    /// <param name="templateSlide">The template slide.</param>
    private static SlidePart TemplateSlidePartOf(Dictionary<Uri, SlidePart> keptSlideParts, HashSet<Uri> usedTemplateSlides, TemplateSlide templateSlide)
    {
        if (!keptSlideParts.TryGetValue(templateSlide.PartUri, out var slidePart))
            throw new InvalidOperationException($"The template has no slide at '{templateSlide.PartUri}'. The template was read from another file than the one being written.");

        if (!usedTemplateSlides.Add(templateSlide.PartUri))
            throw new InvalidOperationException($"The slide deck uses template slide {templateSlide.Number} more than once. Check the deck before writing it.");

        return slidePart;
    }

    /// <summary>
    /// Makes a template slide the deck keeps look like the slides around it: in the color variant
    /// of the presentation, and with its footer.
    /// </summary>
    /// <remarks>
    /// Everything else stays as the author of the template made it, pictures and notes included.
    /// The slide shows even when the template hides it, since the user chose it for the presentation.
    /// </remarks>
    /// <param name="slidePart">The slide part of the template slide.</param>
    /// <param name="templateSlide">The template slide, as read from the template.</param>
    /// <param name="template">The template.</param>
    /// <param name="layoutParts">The layouts of the document being written, by where they are stored.</param>
    /// <param name="variantLabel">The color variant of the presentation; null when the template has no variants.</param>
    /// <param name="footerText">The footer text; empty to keep the footer of the template slide.</param>
    /// <returns>The layout the slide is based on now; null when its layout belongs to no family, which leaves the slide without slots to fill.</returns>
    private static TemplateLayout? AdaptTemplateSlide(SlidePart slidePart, TemplateSlide templateSlide, PresentationTemplate template, Dictionary<Uri, SlideLayoutPart> layoutParts, string? variantLabel, string footerText)
    {
        var slide = slidePart.Slide ?? throw new InvalidDataException($"The template slide {templateSlide.Number} has no content.");
        slide.Show = null;

        TemplateLayout? layout = null;
        if (templateSlide.FamilyId is { } familyId && template.FindFamily(familyId) is { } family && family.Variants.FirstOrDefault(variant => variant.Layout.PartUri == templateSlide.LayoutPartUri)?.Layout is { } currentLayout)
        {
            layout = family.GetLayout(variantLabel);
            if (layout.PartUri != currentLayout.PartUri)
                MoveToLayout(slidePart, slide, currentLayout, layout, layoutParts);
        }

        if (footerText.Length > 0)
            ReplaceFooter(slidePart, slide, footerText);

        return layout;
    }

    /// <summary>
    /// Writes the content the model wrote for a template slide into the slots of that slide.
    /// </summary>
    /// <remarks>
    /// Only slots with content change; every other one keeps what the template slide holds. A text
    /// takes the formatting the slide gave the old one. A picture or a table replaces the old one
    /// as a whole, since nothing of its old shape fits the new content, and a picture nobody shows
    /// any more leaves the file. This holds for a table which is no placeholder as well, so that no
    /// row of the old table shows below a shorter new one. A new picture or table goes where the
    /// slide put the old one, should it have moved it away from where the layout puts it. A slot the
    /// slide does not carry gets its placeholder from the layout, as on a slide written anew. A copy
    /// of a slot takes its new text like a text slot, in the formatting of its old one.
    ///
    /// What the slide removes goes first, so that the slots are filled on what is left.
    /// </remarks>
    /// <param name="slidePart">The slide part of the template slide, already on the layout of SlideContent.Layout.</param>
    /// <param name="templateSlide">The template slide, as read from the template, which knows where it moved its slots, which copies of slots and which elements it carries.</param>
    /// <param name="content">What goes on the slide.</param>
    private static void EditTemplateSlide(SlidePart slidePart, TemplateSlide templateSlide, SlideContent content)
    {
        if (slidePart.Slide?.CommonSlideData?.ShapeTree is not { } shapeTree)
            return;

        RemoveFromTemplateSlide(slidePart, shapeTree, templateSlide, content);

        // Which shape fills which slot, by the same reading as the template's:
        var shapesBySlot = TemplateSlideShapes.SlotShapesOf(shapeTree, content.Layout);

        var layoutPlaceholders = LayoutPlaceholders.Of(content.LayoutElement);
        var slide = content.Slide;
        foreach (var slot in content.Layout.Slots)
        {
            var index = slot.Ordinal - 1;
            var existing = shapesBySlot.GetValueOrDefault(slot);
            var placeholder = existing is not null && TemplateSlideShapes.PlaceholderOf(existing) is { } existingPlaceholder
                ? (P.PlaceholderShape)existingPlaceholder.CloneNode(true)
                : layoutPlaceholders.For(slot);

            var paragraphs = ParagraphsFor(slot, slide);
            OpenXmlElement? replacement;
            if (paragraphs is not null)
            {
                if (existing is P.Shape shape)
                {
                    ReplaceParagraphs(shape, paragraphs);
                    continue;
                }

                var id = NextShapeIdOf(shapeTree);
                var name = slot.Role switch
                {
                    PlaceholderRole.TITLE => "Title",
                    PlaceholderRole.SUBTITLE => "Subtitle",
                    PlaceholderRole.LABEL => "Label",

                    _ => "Text",
                };

                replacement = TextShape(id, $"{name} {id}", placeholder, paragraphs, slot.Role is PlaceholderRole.TEXT);
            }
            else
            {
                var bounds = templateSlide.OnSlide(slot).Bounds;
                replacement = slot.Role switch
                {
                    PlaceholderRole.PICTURE when index < slide.Pictures.Count && content.Pictures.TryGetValue(slide.Pictures[index], out var picture) => PictureShape(NextShapeIdOf(shapeTree), placeholder, slidePart, picture, bounds, bounds.Area >= content.SlideArea * PresentationTemplate.BACKDROP_AREA_SHARE),
                    PlaceholderRole.TABLE when index == 0 && slide.Table is { Headers.Count: > 0 } table => TableShape(NextShapeIdOf(shapeTree), placeholder, table, bounds, content.TableStyleId),

                    _ => null,
                };
            }

            if (replacement is null)
                continue;

            if (existing is null)
            {
                shapeTree.Append(replacement);
                continue;
            }

            // In the place of the old one, so that what lay above it still does:
            existing.InsertAfterSelf(replacement);
            RemoveShape(slidePart, existing);

            //
            // A free table may stand in for an empty placeholder of its slot. The new table fills
            // that placeholder now, so the empty one would only fill the slot a second time:
            //
            if (TemplateSlideShapes.PlaceholderOf(existing) is null)
            {
                foreach (var emptyPlaceholder in shapeTree.Elements().Where(element => element != replacement && TemplateSlideShapes.PlaceholderOf(element) is { } other && content.Layout.SlotOf(other.Type?.InnerText ?? "obj", other.Index?.Value) == slot).ToList())
                    RemoveShape(slidePart, emptyPlaceholder);
            }
        }

        // A copy of a slot keeps its shape, and with it the look of the slot it repeats:
        foreach (var copy in templateSlide.SlotCopies)
        {
            if (ParagraphsFor(copy.Slot, slide) is { } paragraphs && ShapeWithId(shapeTree, copy.ShapeId) is P.Shape shape)
                ReplaceParagraphs(shape, paragraphs);
        }
    }

    /// <summary>
    /// The paragraphs the slide writes into a text slot.
    /// </summary>
    /// <returns>The paragraphs; null when the slot is no text slot, or the slide leaves it empty.</returns>
    private static List<A.Paragraph>? ParagraphsFor(TemplateSlot slot, SlideSpec slide)
    {
        var index = slot.Ordinal - 1;
        return slot.Role switch
        {
            PlaceholderRole.TITLE when slide.Title.Length > 0 => ParagraphsOf(slide.Title),
            PlaceholderRole.SUBTITLE when slide.Subtitle.Length > 0 => ParagraphsOf(slide.Subtitle),
            PlaceholderRole.TEXT when index < slide.Texts.Count && slide.Texts[index].Paragraphs.Count > 0 => slide.Texts[index].Paragraphs.Select(ParagraphOf).ToList(),
            PlaceholderRole.LABEL when index < slide.Labels.Count && slide.Labels[index].Length > 0 => ParagraphsOf(slide.Labels[index]),

            _ => null,
        };
    }

    /// <summary>
    /// The top-level shape of a slide with the given ID.
    /// </summary>
    /// <returns>The shape; null when the slide no longer has it.</returns>
    private static OpenXmlElement? ShapeWithId(P.ShapeTree shapeTree, uint shapeId) => shapeTree.Elements().FirstOrDefault(candidate => TemplateSlideShapes.ShapeIdOf(candidate) == shapeId);

    /// <summary>
    /// Removes what an edited slide no longer shows: the elements, the slot contents and the copies of slots it names.
    /// </summary>
    /// <remarks>
    /// The deck was checked against the template as it was read, so every name is known. A name
    /// which finds nothing, because the file changed in between, is passed over: there is nothing
    /// left to remove.
    /// </remarks>
    private static void RemoveFromTemplateSlide(SlidePart slidePart, P.ShapeTree shapeTree, TemplateSlide templateSlide, SlideContent content)
    {
        if (content.Slide.Removals.Count == 0)
            return;

        var shapesBySlot = TemplateSlideShapes.SlotShapesOf(shapeTree, content.Layout);
        var shapes = new List<OpenXmlElement>();
        foreach (var name in content.Slide.Removals)
        {
            if (templateSlide.FindElement(name) is { } element)
            {
                if (ShapeWithId(shapeTree, element.ShapeId) is { } elementShape)
                    shapes.Add(elementShape);
            }
            else if (PlaceholderRoleExtensions.TryParseSlotName(name, out var role, out var ordinal) && role is not PlaceholderRole.TITLE)
            {
                if (content.Layout.Slots.FirstOrDefault(candidate => candidate.Role == role && candidate.Ordinal == ordinal) is { } slot)
                {
                    if (shapesBySlot.TryGetValue(slot, out var slotShape))
                        shapes.Add(slotShape);
                }
                else if (templateSlide.FindCopy(role, ordinal) is { } copy && ShapeWithId(shapeTree, copy.ShapeId) is { } copyShape)
                    shapes.Add(copyShape);
            }
        }

        foreach (var shape in shapes.Distinct())
            RemoveShape(slidePart, shape);
    }

    /// <summary>
    /// Removes a shape from a slide, together with the parts only it used.
    /// </summary>
    /// <remarks>
    /// A shape refers to parts of the file by relationship IDs: a picture to its image, a chart to
    /// its data, a diagram to its drawing. A part which nothing else on the slide refers to leaves
    /// the slide; the file drops it when no other slide refers to it either. An animation of the
    /// shape would point into nothing afterwards, which PowerPoint repairs by dropping every
    /// animation of the slide, so that is done here right away.
    /// </remarks>
    private static void RemoveShape(SlidePart slidePart, OpenXmlElement shape)
    {
        var relationshipIds = RelationshipIdsOf(shape);
        var shapeIds = shape.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id?.Value.ToString(CultureInfo.InvariantCulture)).OfType<string>().ToHashSet();
        shape.Remove();

        var slide = slidePart.Slide;
        var stillUsed = slide is null ? new HashSet<string>() : RelationshipIdsOf(slide);
        foreach (var relationshipId in relationshipIds.Where(id => !stillUsed.Contains(id)))
        {
            if (slidePart.TryGetPartById(relationshipId, out var part))
                slidePart.DeletePart(part);
        }

        if (slide?.Timing?.Descendants().Any(element => element.GetAttributes().Any(attribute => attribute.LocalName == "spid" && shapeIds.Contains(attribute.Value ?? string.Empty))) is true)
            slide.Timing = null;
    }

    /// <summary>
    /// The relationship IDs an element and everything within it refer to.
    /// </summary>
    private static HashSet<string> RelationshipIdsOf(OpenXmlElement element) => element.Descendants()
        .Prepend(element)
        .SelectMany(candidate => candidate.GetAttributes())
        .Where(attribute => attribute.NamespaceUri == RELATIONSHIPS_NAMESPACE)
        .Select(attribute => attribute.Value)
        .OfType<string>()
        .ToHashSet();

    /// <summary>
    /// Moves a slide to the layout of the same family in another color variant.
    /// </summary>
    /// <remarks>
    /// The variants of a family offer the same slots at the same places, yet not necessarily under
    /// the same placeholder indices. Every placeholder of the slide is therefore linked anew: a slot
    /// to the slot at the same position in reading order, and footer, slide number and date to
    /// those of the new layout. The old layout stays in the file, since its master still lists it.
    /// </remarks>
    private static void MoveToLayout(SlidePart slidePart, P.Slide slide, TemplateLayout currentLayout, TemplateLayout targetLayout, Dictionary<Uri, SlideLayoutPart> layoutParts)
    {
        var targetLayoutPart = LayoutPartOf(layoutParts, targetLayout);
        var targetPlaceholders = LayoutPlaceholders.Of(targetLayoutPart.SlideLayout);
        foreach (var placeholder in slide.CommonSlideData?.ShapeTree?.Descendants<P.PlaceholderShape>().ToList() ?? [])
        {
            var type = placeholder.Type?.InnerText ?? "obj";
            if (currentLayout.SlotOf(type, placeholder.Index?.Value) is { } slot)
            {
                var position = PositionOf(currentLayout.Slots, slot);
                if (position < targetLayout.Slots.Count)
                    LinkTo(placeholder, slot, targetLayout.Slots[position]);

                continue;
            }

            var targetPlaceholder = type switch
            {
                "ftr" => targetPlaceholders.Footer,
                "sldNum" => targetPlaceholders.SlideNumber,
                "dt" => targetPlaceholders.Date,

                _ => null,
            };

            if (targetPlaceholder is not null)
                SetIndex(placeholder, targetPlaceholder.Index?.Value);
        }

        if (slidePart.SlideLayoutPart is { } currentLayoutPart)
            slidePart.DeletePart(currentLayoutPart);

        slidePart.AddPart(targetLayoutPart);
    }

    /// <summary>
    /// Links a placeholder of a slide to the slot of another layout which takes the place of its slot.
    /// </summary>
    /// <remarks>
    /// The type changes only where the two layouts spell it differently: a slide often states no
    /// type at all, which is fine as long as the index matches.
    /// </remarks>
    private static void LinkTo(P.PlaceholderShape placeholder, TemplateSlot slot, TemplateSlot targetSlot)
    {
        SetIndex(placeholder, targetSlot.PlaceholderIndex);
        if (targetSlot.PlaceholderType == slot.PlaceholderType)
            return;

        if (targetSlot.PlaceholderType == "obj")
            placeholder.Type = null;
        else
            placeholder.Type = new P.PlaceholderValues(targetSlot.PlaceholderType);
    }

    private static void SetIndex(P.PlaceholderShape placeholder, uint? index)
    {
        if (index is { } value)
            placeholder.Index = value;
        else
            placeholder.Index = null;
    }

    private static int PositionOf(IReadOnlyList<TemplateSlot> slots, TemplateSlot slot)
    {
        for (var position = 0; position < slots.Count; position++)
            if (slots[position] == slot)
                return position;

        return slots.Count;
    }

    /// <summary>
    /// Writes the footer of the presentation into the footer of a template slide.
    /// </summary>
    /// <remarks>
    /// A template slide without a footer gets one from its layout, like every slide written anew.
    /// </remarks>
    private static void ReplaceFooter(SlidePart slidePart, P.Slide slide, string footerText)
    {
        if (slide.CommonSlideData?.ShapeTree is not { } shapeTree)
            return;

        var footers = shapeTree.Elements<P.Shape>().Where(shape => TemplateSlideShapes.PlaceholderOf(shape)?.Type?.InnerText == "ftr").ToList();
        foreach (var footer in footers)
            ReplaceParagraphs(footer, ParagraphsOf(footerText));

        if (footers.Count == 0 && LayoutPlaceholders.Of(slidePart.SlideLayoutPart?.SlideLayout).Footer is { } layoutFooter)
        {
            var id = NextShapeIdOf(shapeTree);
            shapeTree.Append(TextShape(id, $"Footer {id}", Clone(layoutFooter), ParagraphsOf(footerText), false));
        }
    }

    /// <summary>
    /// Replaces the text of a shape on a template slide, keeping how the slide formats it.
    /// </summary>
    /// <remarks>
    /// The body properties and the list style stay. A new paragraph takes the formatting of the first
    /// old paragraph on its list level: its paragraph properties, e.g., a bullet its author turned
    /// off, and the properties of its first run, e.g., a size or a color picked by hand. A paragraph
    /// without text keeps the latter in its end mark, which is where an author leaves them on a slide
    /// waiting for text.
    /// </remarks>
    private static void ReplaceParagraphs(P.Shape shape, IEnumerable<A.Paragraph> paragraphs)
    {
        var textBody = shape.TextBody ??= new P.TextBody(new A.BodyProperties(), new A.ListStyle());
        var formats = new Dictionary<int, (A.ParagraphProperties? Paragraph, A.RunProperties? Run)>();
        foreach (var oldParagraph in textBody.Elements<A.Paragraph>().ToList())
        {
            formats.TryAdd(LevelOf(oldParagraph), (oldParagraph.ParagraphProperties, RunPropertiesOf(oldParagraph)));
            oldParagraph.Remove();
        }

        foreach (var paragraph in paragraphs)
        {
            if (formats.TryGetValue(LevelOf(paragraph), out var format))
            {
                if (format.Paragraph is not null)
                    paragraph.ParagraphProperties = (A.ParagraphProperties)format.Paragraph.CloneNode(true);

                if (format.Run is not null)
                    foreach (var run in paragraph.Elements<A.Run>().Where(run => run.RunProperties is null))
                        run.RunProperties = (A.RunProperties)format.Run.CloneNode(true);
            }

            textBody.Append(paragraph);
        }
    }

    private static int LevelOf(A.Paragraph paragraph) => paragraph.ParagraphProperties?.Level?.Value ?? 0;

    /// <summary>
    /// How a paragraph formats its text: the properties of its first run, or of its end mark when it has no text.
    /// </summary>
    private static A.RunProperties? RunPropertiesOf(A.Paragraph paragraph)
    {
        if (paragraph.Elements<A.Run>().FirstOrDefault()?.RunProperties is { } runProperties)
            return runProperties;

        if (paragraph.GetFirstChild<A.EndParagraphRunProperties>() is not { } endProperties)
            return null;

        // Both carry the same properties of a text, under another name:
        var converted = new A.RunProperties(endProperties.ChildElements.Select(child => child.CloneNode(true)));
        converted.SetAttributes(endProperties.GetAttributes());
        return converted;
    }

    /// <summary>
    /// The next free shape ID of a slide.
    /// </summary>
    private static uint NextShapeIdOf(P.ShapeTree shapeTree) => shapeTree.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id?.Value ?? 0U).DefaultIfEmpty(1U).Max() + 1;
}