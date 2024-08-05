namespace AIStudio.Tools;

public static class CommonLanguageExtensions
{
    public static string Name(this CommonLanguages language) => language switch
    {
        CommonLanguages.AS_IS => "Do not change the language",
        
        CommonLanguages.EN_US => "English (US)",
        CommonLanguages.EN_GB => "English (UK)",
        CommonLanguages.ZH_CN => "Chinese (Simplified)",
        CommonLanguages.HI_IN => "Hindi (India)",
        CommonLanguages.ES_ES => "Spanish (Spain)",
        CommonLanguages.FR_FR => "French (France)",
        CommonLanguages.DE_DE => "German (Germany)",
        CommonLanguages.DE_AT => "German (Austria)",
        CommonLanguages.DE_CH => "German (Switzerland)",
        CommonLanguages.JA_JP => "Japanese (Japan)",
            
        _ => "Other",
    };
    
    public static string PromptSummarizing(this CommonLanguages language, string customLanguage) => language switch
    {
        CommonLanguages.AS_IS => "Do not change the language of the text.",
        CommonLanguages.OTHER => $"Output your summary in {customLanguage}.",

        _ => $"Output your summary in {language.Name()} ({language}).",
    };
    
    public static string PromptTranslation(this CommonLanguages language, string customLanguage) => language switch
    {
        CommonLanguages.OTHER => $"Translate the text in {customLanguage}.",

        _ => $"Translate the given text in {language.Name()} ({language}).",
    };

    public static string NameSelecting(this CommonLanguages language)
    {
        if(language is CommonLanguages.AS_IS)
            return "Please select the target language";
        
        return language.Name();
    }
}