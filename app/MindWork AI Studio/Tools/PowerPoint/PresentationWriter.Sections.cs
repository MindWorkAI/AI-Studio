using P = DocumentFormat.OpenXml.Presentation;
using P14 = DocumentFormat.OpenXml.Office2010.PowerPoint;

namespace AIStudio.Tools.PowerPoint;

internal static partial class PresentationWriter
{
    /// <summary>
    /// The extension of the presentation which holds its sections, as PowerPoint 2010 introduced them.
    /// </summary>
    private const string SECTION_LIST_URI = "{521415D9-36F7-43E2-AB2F-B90AF26B5E84}";

    private static void RemoveSections(P.Presentation presentation)
    {
        if (presentation.PresentationExtensionList is not { } extensions)
            return;

        foreach (var extension in extensions.Elements<P.PresentationExtension>().Where(candidate => candidate.Uri?.Value == SECTION_LIST_URI).ToList())
            extension.Remove();
    }

    /// <summary>
    /// Makes every chapter a section, so that the slide sorter of PowerPoint shows the outline.
    /// </summary>
    /// <remarks>
    /// The slides before the first chapter, such as the title slide, form a section named after the
    /// presentation. A deck without chapters gets no sections at all.
    /// </remarks>
    /// <param name="presentation">The presentation.</param>
    /// <param name="template">The template, which names the template slides of the deck.</param>
    /// <param name="presentationTitle">The title of the presentation, which names the section before the first chapter.</param>
    /// <param name="slides">The slides of the deck, in the order they were written.</param>
    /// <param name="slideIds">The IDs of these slides, in the same order.</param>
    private static void WriteSections(P.Presentation presentation, PresentationTemplate template, string presentationTitle, IReadOnlyList<SlideSpec> slides, IReadOnlyList<uint> slideIds)
    {
        if (!slides.Any(slide => slide.StartsChapter))
            return;

        var sections = new P14.SectionList();
        P14.SectionSlideIdList? currentSection = null;
        for (var index = 0; index < slides.Count; index++)
        {
            var slide = slides[index];
            if (currentSection is null || slide.StartsChapter)
            {
                var title = TitleOf(slide, template);
                var name = slide.StartsChapter && title.Length > 0 ? title : presentationTitle;
                currentSection = new P14.SectionSlideIdList();
                sections.Append(new P14.Section(currentSection) { Name = name, Id = NewSectionId() });
            }

            currentSection.Append(new P14.SectionSlideIdListEntry { Id = slideIds[index] });
        }

        var extensions = presentation.PresentationExtensionList ??= new P.PresentationExtensionList();
        extensions.InsertAt(new P.PresentationExtension(sections) { Uri = SECTION_LIST_URI }, 0);
    }

    private static string NewSectionId() => $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}";
}