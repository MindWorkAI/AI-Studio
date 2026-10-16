using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

internal static partial class PresentationWriter
{
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
    /// any more leaves the file. A slot the slide does not carry gets its placeholder from the
    /// layout, as on a slide written anew.
    /// </remarks>
    /// <param name="slidePart">The slide part of the template slide, already on the layout of <see cref="SlideContent.Layout"/>.</param>
    /// <param name="content">What goes on the slide.</param>
    private static void EditTemplateSlide(SlidePart slidePart, SlideContent content)
    {
        if (slidePart.Slide?.CommonSlideData?.ShapeTree is not { } shapeTree)
            return;

        // Which shape fills which slot, by the same reading as the template's:
        var shapesBySlot = new Dictionary<TemplateSlot, OpenXmlElement>();
        foreach (var element in shapeTree.Elements())
        {
            if (PlaceholderOf(element) is { } placeholder && content.Layout.SlotOf(placeholder.Type?.InnerText ?? "obj", placeholder.Index?.Value) is { } slot)
                shapesBySlot.TryAdd(slot, element);
        }

        var layoutPlaceholders = LayoutPlaceholders.Of(content.LayoutElement);
        var slide = content.Slide;
        foreach (var slot in content.Layout.Slots)
        {
            var index = slot.Ordinal - 1;
            var existing = shapesBySlot.GetValueOrDefault(slot);
            var placeholder = existing is not null && PlaceholderOf(existing) is { } existingPlaceholder
                ? (P.PlaceholderShape)existingPlaceholder.CloneNode(true)
                : layoutPlaceholders.For(slot);

            List<A.Paragraph>? paragraphs = slot.Role switch
            {
                PlaceholderRole.TITLE when slide.Title.Length > 0 => ParagraphsOf(slide.Title),
                PlaceholderRole.SUBTITLE when slide.Subtitle.Length > 0 => ParagraphsOf(slide.Subtitle),
                PlaceholderRole.TEXT when index < slide.Texts.Count && slide.Texts[index].Paragraphs.Count > 0 => slide.Texts[index].Paragraphs.Select(ParagraphOf).ToList(),
                PlaceholderRole.LABEL when index < slide.Labels.Count && slide.Labels[index].Length > 0 => ParagraphsOf(slide.Labels[index]),

                _ => null,
            };

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
                replacement = slot.Role switch
                {
                    PlaceholderRole.PICTURE when index < slide.Pictures.Count && content.Pictures.TryGetValue(slide.Pictures[index], out var picture) => PictureShape(NextShapeIdOf(shapeTree), placeholder, slidePart, picture, slot.Bounds, slot.Bounds.Area >= content.SlideArea * BACKDROP_AREA_SHARE),
                    PlaceholderRole.TABLE when index == 0 && slide.Table is { Headers.Count: > 0 } table => TableShape(NextShapeIdOf(shapeTree), placeholder, table, slot.Bounds, content.TableStyleId),

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
        }
    }

    /// <summary>
    /// Removes a shape from a slide, together with the pictures only it showed.
    /// </summary>
    private static void RemoveShape(SlidePart slidePart, OpenXmlElement shape)
    {
        var pictureIds = shape.Descendants<A.Blip>().Select(blip => blip.Embed?.Value).OfType<string>().ToList();
        shape.Remove();
        foreach (var pictureId in pictureIds)
        {
            if (slidePart.Slide?.Descendants<A.Blip>().Any(blip => blip.Embed?.Value == pictureId) is not true && slidePart.TryGetPartById(pictureId, out var picturePart))
                slidePart.DeletePart(picturePart);
        }
    }

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

        var footers = shapeTree.Elements<P.Shape>().Where(shape => PlaceholderOf(shape)?.Type?.InnerText == "ftr").ToList();
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
    /// The placeholder a shape, a picture or a table of a slide fills; null for anything else.
    /// </summary>
    private static P.PlaceholderShape? PlaceholderOf(OpenXmlElement element) => element switch
    {
        P.Shape shape => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
        P.Picture picture => picture.NonVisualPictureProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,
        P.GraphicFrame frame => frame.NonVisualGraphicFrameProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape,

        _ => null,
    };

    /// <summary>
    /// The next free shape ID of a slide.
    /// </summary>
    private static uint NextShapeIdOf(P.ShapeTree shapeTree) => shapeTree.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id?.Value ?? 0U).DefaultIfEmpty(1U).Max() + 1;
}