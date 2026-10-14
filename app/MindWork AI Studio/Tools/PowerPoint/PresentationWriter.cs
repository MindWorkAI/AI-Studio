using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;

using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace AIStudio.Tools.PowerPoint;

/// <summary>
/// Writes a presentation from a template and a checked slide deck.
/// </summary>
/// <remarks>
/// The presentation is the template itself, with its slides replaced: masters, layouts, theme, fonts
/// and table styles stay exactly as their author made them. Every new slide is based on a layout of
/// the template and fills that layout's placeholders, without any formatting of its own, so the
/// corporate design applies to everything written here. Slots without content get no placeholder
/// at all, which leaves no prompt text such as "Click to add text" behind.
///
/// Every presentation ends with an imprint, written on a content layout of the template like every
/// other slide. It is no slide of the deck, so the number of slides the user asked for leaves it out.
///
/// The deck has to pass <see cref="SlideDeckSpecValidator"/> first; what fits the template is not
/// checked again here.
/// </remarks>
internal static partial class PresentationWriter
{
    /// <summary>
    /// The smallest slide ID PowerPoint accepts.
    /// </summary>
    private const uint FIRST_SLIDE_ID = 256;

    /// <summary>
    /// The picture formats PowerPoint shows everywhere. WebP, for instance, only newer versions do.
    /// </summary>
    private static readonly HashSet<string> SUPPORTED_PICTURE_TYPES = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/bmp",
        "image/tiff",
    };

    /// <summary>
    /// Writes the presentation.
    /// </summary>
    /// <param name="templateFile">The template file, a .pptx or a .potx, positioned at its start.</param>
    /// <param name="template">The template as read from the same file.</param>
    /// <param name="deck">The slide deck, already checked against the template.</param>
    /// <param name="options">What the user chose for the presentation as a whole.</param>
    /// <param name="pictures">Where the pictures the deck refers to come from.</param>
    /// <param name="output">Where the presentation goes.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>What happened beyond writing.</returns>
    public static async Task<PresentationWriteResult> WriteAsync(Stream templateFile, PresentationTemplate template, SlideDeckSpec deck, PresentationWriteOptions options, IPictureSource pictures, Stream output, CancellationToken token)
    {
        //
        // Loading the pictures is the only part which waits. The document is edited in one go
        // afterwards, since the Open XML SDK works synchronously:
        //
        var (loadedPictures, skippedPictures) = await LoadPicturesAsync(deck, pictures, token);

        using var file = new MemoryStream();
        await templateFile.CopyToAsync(file, token);
        file.Position = 0;

        using (var document = PresentationDocument.Open(file, true))
        {
            // A template (.potx) becomes a presentation; everything else about it stays:
            if (document.DocumentType != PresentationDocumentType.Presentation)
                document.ChangeDocumentType(PresentationDocumentType.Presentation);

            var presentationPart = document.PresentationPart ?? throw new InvalidDataException("The file contains no presentation.");
            var presentation = presentationPart.Presentation ?? throw new InvalidDataException("The file contains no presentation.");

            RemoveSlides(presentationPart, presentation);

            var layoutParts = presentationPart.SlideMasterParts.SelectMany(master => master.SlideLayoutParts).ToDictionary(part => part.Uri);
            var tableStyleId = presentationPart.TableStylesPart?.TableStyleList?.Default?.Value;
            var slideIdList = presentation.SlideIdList ??= new P.SlideIdList();
            var variantLabels = VariantLabelsOf(deck, template, options);
            var slideIds = new List<uint>(deck.Slides.Count);
            for (var index = 0; index < deck.Slides.Count; index++)
            {
                var slide = deck.Slides[index];
                var family = template.FindFamily(slide.Layout) ?? throw new InvalidOperationException($"The slide deck uses the layout '{slide.Layout}', which the template does not have. Check the deck before writing it.");
                var layout = family.GetLayout(variantLabels[index]);
                var layoutPart = LayoutPartOf(layoutParts, layout);
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.AddPart(layoutPart);
                var content = new SlideContent(slide, layout, layoutPart.SlideLayout, loadedPictures, tableStyleId, options.FooterText, (double)template.SlideWidth * template.SlideHeight);
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(ShapeTreeOf(slidePart, content)),
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                var slideId = FIRST_SLIDE_ID + (uint)index;
                slideIdList.Append(new P.SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
                slideIds.Add(slideId);
            }

            //
            // The imprint closes the presentation, in the variant of the title slide. It is no slide
            // of the deck, so the model neither planned nor counted it:
            //
            uint? imprintSlideId = null;
            if (ImprintFamilyOf(template) is { } imprintFamily)
            {
                var layout = imprintFamily.GetLayout(variantLabels.FirstOrDefault());
                var layoutPart = LayoutPartOf(layoutParts, layout);
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.AddPart(layoutPart);
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(ImprintShapeTreeOf(layout, layoutPart.SlideLayout, deck, options.FooterText)),
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                imprintSlideId = FIRST_SLIDE_ID + (uint)deck.Slides.Count;
                slideIdList.Append(new P.SlideId { Id = imprintSlideId.Value, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
            }

            WriteSections(presentation, deck, slideIds, imprintSlideId);
        }

        file.Position = 0;
        await file.CopyToAsync(output, token);
        return new PresentationWriteResult(skippedPictures);
    }

    private static SlideLayoutPart LayoutPartOf(Dictionary<Uri, SlideLayoutPart> layoutParts, TemplateLayout layout) => layoutParts.TryGetValue(layout.PartUri, out var layoutPart)
        ? layoutPart
        : throw new InvalidOperationException($"The template has no layout at '{layout.PartUri}'. The template was read from another file than the one being written.");

    private static async Task<(Dictionary<int, SlidePicture> Loaded, List<int> Skipped)> LoadPicturesAsync(SlideDeckSpec deck, IPictureSource pictures, CancellationToken token)
    {
        var loaded = new Dictionary<int, SlidePicture>();
        var skipped = new List<int>();
        foreach (var number in deck.Slides.SelectMany(slide => slide.Pictures).Distinct())
        {
            var picture = await pictures.LoadAsync(number, token);
            if (picture is { Width: > 0, Height: > 0 } && SUPPORTED_PICTURE_TYPES.Contains(picture.ContentType))
                loaded[number] = picture;
            else
                skipped.Add(number);
        }

        return (loaded, skipped);
    }

    /// <summary>
    /// Removes the slides of the template, which are samples or hints for the user.
    /// </summary>
    /// <remarks>
    /// Whatever lists slides by their IDs has to go with them, or PowerPoint finds references to
    /// nothing and offers to repair the file: custom shows and sections. Sections are written anew
    /// for the new slides.
    /// </remarks>
    private static void RemoveSlides(PresentationPart presentationPart, P.Presentation presentation)
    {
        foreach (var slideId in presentation.SlideIdList?.Elements<P.SlideId>().ToList() ?? [])
        {
            if (slideId.RelationshipId?.Value is { } relationshipId && presentationPart.TryGetPartById(relationshipId, out var slidePart))
                presentationPart.DeletePart(slidePart);

            slideId.Remove();
        }

        presentation.CustomShowList = null;
        RemoveSections(presentation);
    }

    /// <summary>
    /// Says which variant each slide is written in.
    /// </summary>
    /// <remarks>
    /// The slides before the first chapter, such as the title slide, share the variant of the
    /// first chapter. When the variant changes per chapter, every further chapter moves on to the
    /// next one, and after the last it starts over.
    /// </remarks>
    private static List<string?> VariantLabelsOf(SlideDeckSpec deck, PresentationTemplate template, PresentationWriteOptions options)
    {
        var labels = template.VariantLabels;
        if (labels.Count == 0)
            return deck.Slides.Select(_ => (string?)null).ToList();

        var start = 0;
        for (var index = 0; index < labels.Count; index++)
            if (string.Equals(labels[index], options.VariantLabel, StringComparison.Ordinal))
                start = index;

        var chapters = 0;
        var result = new List<string?>(deck.Slides.Count);
        foreach (var slide in deck.Slides)
        {
            if (slide.StartsChapter)
                chapters++;

            var offset = options.ChangeVariantPerChapter ? Math.Max(0, chapters - 1) : 0;
            result.Add(labels[(start + offset) % labels.Count]);
        }

        return result;
    }

    /// <summary>
    /// Everything needed to fill the shape tree of one slide.
    /// </summary>
    /// <param name="Slide">What goes on the slide.</param>
    /// <param name="Layout">The layout the slide is based on, as read from the template.</param>
    /// <param name="LayoutElement">The same layout in the document being written, whose placeholders the slide refers to.</param>
    /// <param name="Pictures">The loaded pictures, by their numbers.</param>
    /// <param name="TableStyleId">The default table style of the template, if it states one.</param>
    /// <param name="FooterText">The footer text; empty for no footer.</param>
    /// <param name="SlideArea">The area of a slide, in square EMU.</param>
    private sealed record SlideContent(
        SlideSpec Slide,
        TemplateLayout Layout,
        P.SlideLayout? LayoutElement,
        IReadOnlyDictionary<int, SlidePicture> Pictures,
        string? TableStyleId,
        string FooterText,
        double SlideArea);
}