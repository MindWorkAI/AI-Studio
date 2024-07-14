using AIStudio.Tools;

namespace AIStudio.Components.Pages.TextSummarizer;

public static class CommonLanguagePrompts
{
    public static string Prompt(this CommonLanguages language, string customLanguage) => language switch
    {
        CommonLanguages.AS_IS => "Do not change the language of the text.",
        CommonLanguages.OTHER => $"Output you summary in {customLanguage}.",

        _ => $"Output your summary in {language.Name()} ({language}).",
    };
}