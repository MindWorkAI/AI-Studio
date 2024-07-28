using AIStudio.Tools;

namespace AIStudio.Components.Pages.Translation;

public static class CommonLanguageExtension
{
    public static string Prompt(this CommonLanguages language, string customLanguage) => language switch
    {
        CommonLanguages.OTHER => $"Translate the text in {customLanguage}.",

        _ => $"Translate the given text in {language.Name()} ({language}).",
    };
}