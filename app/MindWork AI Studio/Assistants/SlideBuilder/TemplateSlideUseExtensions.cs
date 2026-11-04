using AIStudio.Tools.PowerPoint;

namespace AIStudio.Assistants.SlideBuilder;

internal static class TemplateSlideUseExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(TemplateSlideUseExtensions).Namespace, nameof(TemplateSlideUseExtensions));

    extension(TemplateSlideUse use)
    {
        public string Name() => use switch
        {
            TemplateSlideUse.OMIT => TB("Leave out"),
            TemplateSlideUse.KEEP => TB("Keep unchanged"),
            TemplateSlideUse.EDIT => TB("Let the LLM edit it"),

            _ => TB("Leave out"),
        };
    }
}