using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Fireworks;
using AIStudio.Provider.Mistral;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;
using AIStudio.Settings;

namespace AIStudio.Provider;

public static class ProvidersExtensions
{
    /// <summary>
    /// Returns the human-readable name of the provider.
    /// </summary>
    /// <param name="llmProvider">The provider.</param>
    /// <returns>The human-readable name of the provider.</returns>
    public static string ToName(this LLMProviders llmProvider) => llmProvider switch
    {
        LLMProviders.NONE => "No provider selected",
        
        LLMProviders.OPEN_AI => "OpenAI",
        LLMProviders.ANTHROPIC => "Anthropic",
        LLMProviders.MISTRAL => "Mistral",
        
        LLMProviders.FIREWORKS => "Fireworks.ai",
        
        LLMProviders.SELF_HOSTED => "Self-hosted",
        
        _ => "Unknown",
    };
    
    /// <summary>
    /// Get a provider's confidence.
    /// </summary>
    /// <param name="llmProvider">The provider.</param>
    /// <param name="settingsManager">The settings manager.</param>
    /// <returns>The confidence of the provider.</returns>
    public static Confidence GetConfidence(this LLMProviders llmProvider, SettingsManager settingsManager) => llmProvider switch
    {
        LLMProviders.NONE => Confidence.NONE,
        
        LLMProviders.FIREWORKS => Confidence.USA_NOT_TRUSTED.WithRegion("America, U.S.").WithSources("https://fireworks.ai/terms-of-service").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.OPEN_AI => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources(
            "https://platform.openai.com/docs/models/default-usage-policies-by-endpoint",
            "https://openai.com/policies/terms-of-use/",
            "https://help.openai.com/en/articles/5722486-how-your-data-is-used-to-improve-model-performance",
            "https://openai.com/enterprise-privacy/"
        ).WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.ANTHROPIC => Confidence.USA_NO_TRAINING.WithRegion("America, U.S.").WithSources("https://www.anthropic.com/legal/commercial-terms").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.MISTRAL => Confidence.GDPR_NO_TRAINING.WithRegion("Europe, France").WithSources("https://mistral.ai/terms/#terms-of-service-la-plateforme").WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        LLMProviders.SELF_HOSTED => Confidence.SELF_HOSTED.WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
        
        _ => Confidence.UNKNOWN.WithLevel(settingsManager.GetConfiguredConfidenceLevel(llmProvider)),
    };

    /// <summary>
    /// Creates a new provider instance based on the provider value.
    /// </summary>
    /// <param name="providerSettings">The provider settings.</param>
    /// <param name="logger">The logger to use.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this Settings.Provider providerSettings, ILogger logger)
    {
        try
        {
            return providerSettings.UsedLLMProvider switch
            {
                LLMProviders.OPEN_AI => new ProviderOpenAI(logger) { InstanceName = providerSettings.InstanceName },
                LLMProviders.ANTHROPIC => new ProviderAnthropic(logger) { InstanceName = providerSettings.InstanceName },
                LLMProviders.MISTRAL => new ProviderMistral(logger) { InstanceName = providerSettings.InstanceName },
                
                LLMProviders.FIREWORKS => new ProviderFireworks(logger) { InstanceName = providerSettings.InstanceName },
                
                LLMProviders.SELF_HOSTED => new ProviderSelfHosted(logger, providerSettings) { InstanceName = providerSettings.InstanceName },
                
                _ => new NoProvider(),
            };
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to create provider: {e.Message}");
            return new NoProvider();
        }
    }
}