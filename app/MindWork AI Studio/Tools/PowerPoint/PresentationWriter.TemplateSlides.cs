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
    private static void AdaptTemplateSlide(SlidePart slidePart, TemplateSlide templateSlide, PresentationTemplate template, Dictionary<Uri, SlideLayoutPart> layoutParts, string? variantLabel, string footerText)
    {
        var slide = slidePart.Slide ?? throw new InvalidDataException($"The template slide {templateSlide.Number} has no content.");
        slide.Show = null;

        if (templateSlide.FamilyId is { } familyId && template.FindFamily(familyId) is { } family && family.Variants.FirstOrDefault(variant => variant.Layout.PartUri == templateSlide.LayoutPartUri)?.Layout is { } currentLayout)
        {
            var targetLayout = family.GetLayout(variantLabel);
            if (targetLayout.PartUri != currentLayout.PartUri)
                MoveToLayout(slidePart, slide, currentLayout, targetLayout, layoutParts);
        }

        if (footerText.Length > 0)
            ReplaceFooter(slidePart, slide, footerText);
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
    /// The body properties and the list style stay. The new runs take over the properties of the
    /// first run the shape had, e.g., a color its author picked by hand.
    /// </remarks>
    private static void ReplaceParagraphs(P.Shape shape, IEnumerable<A.Paragraph> paragraphs)
    {
        var textBody = shape.TextBody ??= new P.TextBody(new A.BodyProperties(), new A.ListStyle());
        var runProperties = textBody.Descendants<A.RunProperties>().FirstOrDefault();
        foreach (var paragraph in textBody.Elements<A.Paragraph>().ToList())
            paragraph.Remove();

        foreach (var paragraph in paragraphs)
        {
            if (runProperties is not null)
                foreach (var run in paragraph.Elements<A.Run>().Where(run => run.RunProperties is null))
                    run.RunProperties = (A.RunProperties)runProperties.CloneNode(true);

            textBody.Append(paragraph);
        }
    }

    private static P.PlaceholderShape? PlaceholderOf(P.Shape shape) => shape.NonVisualShapeProperties?.ApplicationNonVisualDrawingProperties?.PlaceholderShape;

    /// <summary>
    /// The next free shape ID of a slide.
    /// </summary>
    private static uint NextShapeIdOf(P.ShapeTree shapeTree) => shapeTree.Descendants<P.NonVisualDrawingProperties>().Select(properties => properties.Id?.Value ?? 0U).DefaultIfEmpty(1U).Max() + 1;
}