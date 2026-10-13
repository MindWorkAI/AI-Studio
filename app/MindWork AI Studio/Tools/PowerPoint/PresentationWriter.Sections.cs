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
    private static void WriteSections(P.Presentation presentation, SlideDeckSpec deck, IReadOnlyList<uint> slideIds)
    {
        if (!deck.Slides.Any(slide => slide.StartsChapter))
            return;

        var sections = new P14.SectionList();
        P14.SectionSlideIdList? currentSection = null;
        for (var index = 0; index < deck.Slides.Count; index++)
        {
            var slide = deck.Slides[index];
            if (currentSection is null || slide.StartsChapter)
            {
                var name = slide.StartsChapter && slide.Title.Length > 0 ? slide.Title : deck.Title;
                currentSection = new P14.SectionSlideIdList();
                sections.Append(new P14.Section(currentSection) { Name = name, Id = $"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}" });
            }

            currentSection.Append(new P14.SectionSlideIdListEntry { Id = slideIds[index] });
        }

        var extensions = presentation.PresentationExtensionList ??= new P.PresentationExtensionList();
        extensions.InsertAt(new P.PresentationExtension(sections) { Uri = SECTION_LIST_URI }, 0);
    }
}