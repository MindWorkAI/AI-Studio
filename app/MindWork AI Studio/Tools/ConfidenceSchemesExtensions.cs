using AIStudio.Tools.PluginSystem;

namespace AIStudio.Tools;

public static class ConfidenceSchemesExtensions
{
    private static string TB(string fallbackEN) => I18N.I.T(fallbackEN, typeof(ConfidenceSchemesExtensions).Namespace, nameof(ConfidenceSchemesExtensions));
    
    public static string GetListDescription(this ConfidenceSchemes scheme) => scheme switch
    {
        ConfidenceSchemes.TRUST_ALL => TB("Trust all LLM providers"),
        ConfidenceSchemes.TRUST_USA_EUROPE => TB("Trust LLM providers from the USA and Europe"),
        ConfidenceSchemes.TRUST_USA => TB("Trust LLM providers from the USA"),
        ConfidenceSchemes.TRUST_EUROPE => TB("Trust LLM providers from Europe"),
        ConfidenceSchemes.TRUST_ASIA => TB("Trust LLM providers from Asia"),
        ConfidenceSchemes.LOCAL_TRUST_ONLY => TB("Trust only local LLM providers"),
        
        ConfidenceSchemes.CUSTOM => TB("Configure your own confidence scheme"),
        
        _ => TB("Unknown confidence scheme"),
    };
}