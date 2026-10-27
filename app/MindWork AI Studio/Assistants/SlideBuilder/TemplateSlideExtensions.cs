using AIStudio.Tools.PowerPoint;

namespace AIStudio.Assistants.SlideBuilder;

internal static class TemplateSlideExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(TemplateSlideExtensions).Namespace, nameof(TemplateSlideExtensions));

    /// <summary>
    /// How many characters of its title a slide of the template shows in its label.
    /// </summary>
    private const int MAX_SLIDE_TITLE_CHARACTERS = 60;

    /// <summary>
    /// How the forms name a slide of the template: by its number and its title, and what is special about it.
    /// </summary>
    /// <remarks>
    /// The title tells the slides apart at a glance, since a template rarely gives its slides more
    /// than a title. A long one is cut, so that the label stays on one line.
    /// </remarks>
    public static string Label(this TemplateSlide slide)
    {
        var title = slide.Title.Length > MAX_SLIDE_TITLE_CHARACTERS ? $"{slide.Title[..MAX_SLIDE_TITLE_CHARACTERS].TrimEnd()}…" : slide.Title;
        var label = title.Length > 0 ? string.Format(TB("Slide {0}: {1}"), slide.Number, title) : string.Format(TB("Slide {0}"), slide.Number);
        if (slide.IsHidden)
            label = $"{label} ({TB("hidden")})";

        return label;
    }
}