using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    /// <summary>
    /// Get the capabilities of the model used by the configured provider.
    /// </summary>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The capabilities of the configured model.</returns>
    public static List<Capability> GetModelCapabilities(this Provider provider) => provider.UsedLLMProvider.GetModelCapabilities(provider.Model);
    
    /// <summary>
    /// Get the capabilities of a model for a specific provider.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="model">The model to get the capabilities for.</param>
    /// <returns>>The capabilities of the model.</returns>
    public static List<Capability> GetModelCapabilities(this LLMProviders provider, Model model) => provider switch
    {
        LLMProviders.OPEN_AI => GetModelCapabilitiesOpenAI(model),
        LLMProviders.MISTRAL => GetModelCapabilitiesMistral(model),
        LLMProviders.ANTHROPIC => GetModelCapabilitiesAnthropic(model),
        LLMProviders.GOOGLE => GetModelCapabilitiesGoogle(model),
        LLMProviders.X => GetModelCapabilitiesOpenSource(model),
        LLMProviders.DEEP_SEEK => GetModelCapabilitiesDeepSeek(model),
        LLMProviders.ALIBABA_CLOUD => GetModelCapabilitiesAlibaba(model),
        LLMProviders.PERPLEXITY => GetModelCapabilitiesPerplexity(model),
        LLMProviders.OPEN_ROUTER => GetModelCapabilitiesOpenRouter(model),

        LLMProviders.GROQ => GetModelCapabilitiesOpenSource(model),
        LLMProviders.FIREWORKS => GetModelCapabilitiesOpenSource(model),
        LLMProviders.HUGGINGFACE => GetModelCapabilitiesOpenSource(model),
        
        LLMProviders.HELMHOLTZ => GetModelCapabilitiesOpenSource(model),
        LLMProviders.GWDG => GetModelCapabilitiesOpenSource(model),
        
        LLMProviders.SELF_HOSTED => GetModelCapabilitiesOpenSource(model),
        
        _ => []
    };
}