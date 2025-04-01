namespace AIStudio.Assistants.ERI;

public static class AllowedLLMProvidersExtensions
{
    public static string Description(this AllowedLLMProviders provider) => provider switch
    {
        AllowedLLMProviders.NONE => "Please select what kind of LLM provider are allowed for this data source",
        AllowedLLMProviders.ANY => "Any LLM provider is allowed: users might choose a cloud-based or a self-hosted provider",
        AllowedLLMProviders.SELF_HOSTED => "Self-hosted LLM providers are allowed: users cannot choose any cloud-based provider",

        _ => "Unknown option was selected"
    };
}