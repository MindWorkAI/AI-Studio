using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class CommonLanguageExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(CommonLanguageExtensions).Namespace, nameof(CommonLanguageExtensions));
    
    public static string Name(this CommonLanguages language) => language switch
    {
        CommonLanguages.AS_IS => TB("Do not change the language"),
        
        CommonLanguages.EN_US => TB("English (US)"),
        CommonLanguages.EN_GB => TB("English (UK)"),
        CommonLanguages.ZH_CN => TB("Chinese (Simplified)"),
        CommonLanguages.HI_IN => TB("Hindi (India)"),
        CommonLanguages.ES_ES => TB("Spanish (Spain)"),
        CommonLanguages.FR_FR => TB("French (France)"),
        CommonLanguages.DE_DE => TB("German (Germany)"),
        CommonLanguages.DE_AT => TB("German (Austria)"),
        CommonLanguages.DE_CH => TB("German (Switzerland)"),
        CommonLanguages.JA_JP => TB("Japanese (Japan)"),
        CommonLanguages.RU_RU => TB("Russian (Russia)"),
            
        _ => TB("Other"),
    };
    
    public static string ToIETFTag(this CommonLanguages language) => language switch
    {
        CommonLanguages.AS_IS => string.Empty,
        
        CommonLanguages.EN_US => "en-US",
        CommonLanguages.EN_GB => "en-GB",
        CommonLanguages.ZH_CN => "zh-CN",
        CommonLanguages.HI_IN => "hi-IN",
        CommonLanguages.ES_ES => "es-ES",
        CommonLanguages.FR_FR => "fr-FR",
        CommonLanguages.DE_DE => "de-DE",
        CommonLanguages.DE_AT => "de-AT",
        CommonLanguages.DE_CH => "de-CH",
        CommonLanguages.JA_JP => "ja-JP",
        CommonLanguages.RU_RU => "ru-RU",

        _ => string.Empty,
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
            return TB("Please select the target language");
        
        return language.Name();
    }
    
    public static string NameSelectingOptional(this CommonLanguages language)
    {
        if(language is CommonLanguages.AS_IS)
            return TB("Do not specify the language");
        
        return language.Name();
    }
}