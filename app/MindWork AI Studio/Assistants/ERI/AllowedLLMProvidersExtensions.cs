namespace AIStudio.Assistants.ERI;

public static class AllowedLLMProvidersExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AllowedLLMProvidersExtensions).Namespace, nameof(AllowedLLMProvidersExtensions));
    
    public static string Description(this AllowedLLMProviders provider) => provider switch
    {
        AllowedLLMProviders.NONE => TB("Please select what kind of LLM provider are allowed for this data source"),
        AllowedLLMProviders.ANY => TB("Any LLM provider is allowed: users might choose a cloud-based or a self-hosted provider"),
        AllowedLLMProviders.SELF_HOSTED => TB("Self-hosted LLM providers are allowed: users cannot choose any cloud-based provider"),

        _ => TB("Unknown option was selected")
    };
}