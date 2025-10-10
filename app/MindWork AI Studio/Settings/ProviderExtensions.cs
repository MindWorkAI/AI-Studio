using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    public static List<Capability> GetModelCapabilities(this Provider provider) => provider.UsedLLMProvider switch
    {
        LLMProviders.OPEN_AI => GetModelCapabilitiesOpenAI(provider.Model),
        LLMProviders.MISTRAL => GetModelCapabilitiesMistral(provider.Model),
        LLMProviders.ANTHROPIC => GetModelCapabilitiesAnthropic(provider.Model),
        LLMProviders.GOOGLE => GetModelCapabilitiesGoogle(provider.Model),
        LLMProviders.X => GetModelCapabilitiesOpenSource(provider.Model),
        LLMProviders.DEEP_SEEK => GetModelCapabilitiesDeepSeek(provider.Model),
        LLMProviders.ALIBABA_CLOUD => GetModelCapabilitiesAlibaba(provider.Model),
        LLMProviders.PERPLEXITY => GetModelCapabilitiesPerplexity(provider.Model),
     
        LLMProviders.GROQ => GetModelCapabilitiesOpenSource(provider.Model),
        LLMProviders.FIREWORKS => GetModelCapabilitiesOpenSource(provider.Model),
        LLMProviders.HUGGINGFACE => GetModelCapabilitiesOpenSource(provider.Model),
        
        LLMProviders.HELMHOLTZ => GetModelCapabilitiesOpenSource(provider.Model),
        LLMProviders.GWDG => GetModelCapabilitiesOpenSource(provider.Model),
        
        LLMProviders.SELF_HOSTED => GetModelCapabilitiesOpenSource(provider.Model),
        
        _ => []
    };
}