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
/// The template slides the deck refers to stay, with their pictures and notes. They take the color
/// variant and the footer of the presentation, so that they look like the slides around them. A
/// slide the model edited takes its new content into its slots, and keeps the rest.
///
/// Every presentation ends with an imprint, written on a content layout of the template like every
/// other slide. It is no slide of the deck, so the number of slides the user asked for leaves it out.
/// A deck which keeps the imprint of the template gets no second one.
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

            // The imprint of the template closes the presentation, wherever the model put it:
            var slides = deck.Slides.OrderBy(template.IsImprint).ToList();
            var keptSlideParts = RemoveSlides(presentationPart, presentation, TemplateSlidePartUrisOf(template, slides));

            var layoutParts = presentationPart.SlideMasterParts.SelectMany(master => master.SlideLayoutParts).ToDictionary(part => part.Uri);
            var tableStyleId = presentationPart.TableStylesPart?.TableStyleList?.Default?.Value;
            var slideIdList = presentation.SlideIdList ??= new P.SlideIdList();
            var variantLabel = VariantLabelOf(template, options);
            var slideIds = new List<uint>(slides.Count);
            var imprintSlideIds = new List<uint>();
            var imprintName = ImprintTextsOf(deck).Title;
            var usedTemplateSlides = new HashSet<Uri>();
            var slideArea = (double)template.SlideWidth * template.SlideHeight;
            for (var index = 0; index < slides.Count; index++)
            {
                var slide = slides[index];
                SlidePart slidePart;
                if (slide.TemplateSlide is { } number)
                {
                    var templateSlide = template.FindSlide(number) ?? throw new InvalidOperationException($"The slide deck uses template slide {number}, which the template does not have. Check the deck before writing it.");
                    slidePart = TemplateSlidePartOf(keptSlideParts, usedTemplateSlides, templateSlide);
                    var layout = AdaptTemplateSlide(slidePart, templateSlide, template, layoutParts, variantLabel, options.FooterText);
                    if (slide.HasContent)
                    {
                        if (layout is null)
                            throw new InvalidOperationException($"The slide deck writes content into template slide {number}, whose layout has no slots for it. Check the deck before writing it.");

                        EditTemplateSlide(slidePart, templateSlide, new SlideContent(slide, layout, slidePart.SlideLayoutPart?.SlideLayout, loadedPictures, tableStyleId, options.FooterText, slideArea));
                    }
                }
                else
                {
                    var family = template.FindFamily(slide.Layout) ?? throw new InvalidOperationException($"The slide deck uses the layout '{slide.Layout}', which the template does not have. Check the deck before writing it.");
                    var layout = family.GetLayout(variantLabel);
                    var layoutPart = LayoutPartOf(layoutParts, layout);
                    slidePart = presentationPart.AddNewPart<SlidePart>();
                    slidePart.AddPart(layoutPart);
                    var content = new SlideContent(slide, layout, layoutPart.SlideLayout, loadedPictures, tableStyleId, options.FooterText, slideArea);
                    slidePart.Slide = new P.Slide(
                        new P.CommonSlideData(ShapeTreeOf(slidePart, content)),
                        new P.ColorMapOverride(new A.MasterColorMapping()));
                }

                var slideId = FIRST_SLIDE_ID + (uint)index;
                slideIdList.Append(new P.SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
                slideIds.Add(slideId);
                if (template.IsImprint(slide))
                {
                    imprintSlideIds.Add(slideId);
                    if (TitleOf(slide, template) is { Length: > 0 } title)
                        imprintName = title;
                }
            }

            //
            // The imprint closes the presentation. It is no slide of the deck, so the model neither
            // planned nor counted it. A deck which keeps the imprint of the template needs no other:
            //
            if (imprintSlideIds.Count == 0 && ImprintFamilyOf(template) is { } imprintFamily)
            {
                var layout = imprintFamily.GetLayout(variantLabel);
                var layoutPart = LayoutPartOf(layoutParts, layout);
                var slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.AddPart(layoutPart);
                slidePart.Slide = new P.Slide(
                    new P.CommonSlideData(ImprintShapeTreeOf(layout, layoutPart.SlideLayout, deck, options.FooterText)),
                    new P.ColorMapOverride(new A.MasterColorMapping()));

                var imprintSlideId = FIRST_SLIDE_ID + (uint)slides.Count;
                slideIdList.Append(new P.SlideId { Id = imprintSlideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
                imprintSlideIds.Add(imprintSlideId);
            }

            WriteSections(presentation, template, deck.Title, slides, slideIds, imprintSlideIds, imprintName);
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
    /// Removes the slides of the template, which are samples or hints for the user, except those the deck keeps.
    /// </summary>
    /// <remarks>
    /// Whatever lists slides by their IDs has to go with them, or PowerPoint finds references to
    /// nothing and offers to repair the file: custom shows and sections. Sections are written anew
    /// for the new slides. The kept slides lose their place in the list as well, and get it back in
    /// the order of the deck.
    /// </remarks>
    /// <param name="presentationPart">The presentation part, which holds the slides.</param>
    /// <param name="presentation">The presentation, which lists the slides.</param>
    /// <param name="keptSlides">Where the slides the deck keeps are stored inside the file.</param>
    /// <returns>The slides the deck keeps, by where they are stored.</returns>
    private static Dictionary<Uri, SlidePart> RemoveSlides(PresentationPart presentationPart, P.Presentation presentation, IReadOnlySet<Uri> keptSlides)
    {
        var keptSlideParts = new Dictionary<Uri, SlidePart>();
        foreach (var slideId in presentation.SlideIdList?.Elements<P.SlideId>().ToList() ?? [])
        {
            if (slideId.RelationshipId?.Value is { } relationshipId && presentationPart.TryGetPartById(relationshipId, out var slidePart))
            {
                if (slidePart is SlidePart keptSlidePart && keptSlides.Contains(keptSlidePart.Uri))
                    keptSlideParts.TryAdd(keptSlidePart.Uri, keptSlidePart);
                else
                    presentationPart.DeletePart(slidePart);
            }

            slideId.Remove();
        }

        presentation.CustomShowList = null;
        RemoveSections(presentation);
        return keptSlideParts;
    }

    /// <summary>
    /// The title of a slide of the deck: its own, or that of the template slide it keeps.
    /// </summary>
    private static string TitleOf(SlideSpec slide, PresentationTemplate template) => slide.Title.Length > 0
        ? slide.Title
        : slide.TemplateSlide is { } number ? template.FindSlide(number)?.Title ?? string.Empty : string.Empty;

    /// <summary>
    /// Says which variant all slides are written in: the one the user chose, or the first one of
    /// the template when it has no such variant.
    /// </summary>
    /// <returns>The label of the variant; null when the template carries every layout once.</returns>
    private static string? VariantLabelOf(PresentationTemplate template, PresentationWriteOptions options) =>
        options.VariantLabel is { } label && template.VariantLabels.Contains(label)
            ? label
            : template.VariantLabels.FirstOrDefault();

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