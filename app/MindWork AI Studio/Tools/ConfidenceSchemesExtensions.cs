namespace AIStudio.Tools;

public static class ConfidenceSchemesExtensions
{
    public static string GetListDescription(this ConfidenceSchemes scheme) => scheme switch
    {
        ConfidenceSchemes.TRUST_ALL => "Trust all LLM providers",
        ConfidenceSchemes.TRUST_USA_EUROPE => "Trust LLM providers from the USA and Europe",
        ConfidenceSchemes.TRUST_USA => "Trust LLM providers from the USA",
        ConfidenceSchemes.TRUST_EUROPE => "Trust LLM providers from Europe",
        ConfidenceSchemes.TRUST_ASIA => "Trust LLM providers from Asia",
        ConfidenceSchemes.LOCAL_TRUST_ONLY => "Trust only local LLM providers",
        
        ConfidenceSchemes.CUSTOM => "Configure your own confidence scheme",
        
        _ => "Unknown confidence scheme"
    };
}