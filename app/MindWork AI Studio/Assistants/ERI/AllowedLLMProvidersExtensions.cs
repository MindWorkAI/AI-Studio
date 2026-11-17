namespace AIStudio.Assistants.ERI;

public static class AllowedLLMProvidersExtensions
{
    private static string TB(string fallbackEN) => Tools.PluginSystem.I18N.I.T(fallbackEN, typeof(AllowedLLMProvidersExtensions).Namespace, nameof(AllowedLLMProvidersExtensions));

    private static string TA(string fallbackEN) => Tools.PluginSystem.I18N.I.TA(fallbackEN, typeof(AllowedLLMProvidersExtensions).Namespace, nameof(AllowedLLMProvidersExtensions));
    
    extension(AllowedLLMProviders provider)
    {
        public string Description => provider switch
        {
            AllowedLLMProviders.NONE => TA("Please select which kinds of LLM providers are allowed for this data source."),
            AllowedLLMProviders.ANY => TB("Any LLM provider is allowed: users might choose a cloud-based or a self-hosted provider"),
            AllowedLLMProviders.SELF_HOSTED => TB("Self-hosted LLM providers are allowed: users cannot choose any cloud-based provider"),

            _ => TB("Unknown option was selected")
        };
    }
}