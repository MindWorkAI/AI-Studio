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
}