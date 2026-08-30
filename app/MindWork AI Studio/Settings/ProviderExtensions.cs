using AIStudio.Provider;

namespace AIStudio.Settings;

public static partial class ProviderExtensions
{
    /// <summary>
    /// Get the capabilities of the model used by the configured provider.
    /// </summary>
    /// <param name="provider">The configured provider.</param>
    /// <returns>The capabilities of the configured model.</returns>
    public static List<Capability> GetModelCapabilities(this Provider provider)
    {
        var automaticCapabilities = provider.UsedLLMProvider.GetModelCapabilities(provider.Model);
        return provider.CapabilityOverrides?.ApplyTo(automaticCapabilities) ?? automaticCapabilities;
    }
    
    /// <summary>
    /// Get whether the model used by the configured provider accepts images as input.
    /// </summary>
    /// <remarks>
    /// Two capabilities express image input, one for a single image and one for several. Anything that
    /// wants to know whether an image may be sent has to accept both, which is why the question is asked
    /// here instead of at each call site: attaching a file and validating an already attached file must
    /// never disagree about it.
    /// </remarks>
    /// <param name="provider">The configured provider.</param>
    /// <returns><c>true</c> when the model accepts image input.</returns>
    public static bool SupportsImageInput(this Provider provider)
    {
        var capabilities = provider.GetModelCapabilities();
        return capabilities.Contains(Capability.SINGLE_IMAGE_INPUT) || capabilities.Contains(Capability.MULTIPLE_IMAGE_INPUT);
    }

    /// <summary>
    /// Get the capabilities of a model for a specific provider.
    /// </summary>
    /// <param name="provider">The LLM provider.</param>
    /// <param name="model">The model to get the capabilities for.</param>
    /// <returns>>The capabilities of the model.</returns>
    public static List<Capability> GetModelCapabilities(this LLMProviders provider, Model model)
    {
        if (string.IsNullOrWhiteSpace(model.Id))
            return [];

        return provider switch
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
            LLMProviders.HETZNER or LLMProviders.IONOS => GetModelCapabilitiesOpenSource(model),
            
            //
            // LiteLLM is a gateway just like OpenRouter, and it names its models the same way:
            // "vendor/model", e.g. "anthropic/claude-opus-5" or "azure/gpt-5.6". So we let the
            // OpenRouter detection handle it, which resolves the vendor prefix and asks the
            // provider who really knows the model. Everything it cannot place is treated as
            // an open source model, which is the right fallback for a freely named alias:
            //
            LLMProviders.LITE_LLM => GetModelCapabilitiesOpenRouter(model),

            LLMProviders.GROQ => GetModelCapabilitiesOpenSource(model),
            LLMProviders.FIREWORKS => GetModelCapabilitiesOpenSource(model),
            LLMProviders.HUGGINGFACE => GetModelCapabilitiesOpenSource(model),
        
            LLMProviders.HELMHOLTZ => GetModelCapabilitiesOpenSource(model),
            LLMProviders.GWDG => GetModelCapabilitiesOpenSource(model),
        
            LLMProviders.SELF_HOSTED => GetModelCapabilitiesOpenSource(model),
        
            _ => []
        };
    }
}