using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Fireworks;
using AIStudio.Provider.Mistral;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;

namespace AIStudio.Provider;

public static class ProvidersExtensions
{
    /// <summary>
    /// Returns the human-readable name of the provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The human-readable name of the provider.</returns>
    public static string ToName(this Providers provider) => provider switch
    {
        Providers.NONE => "No provider selected",
        
        Providers.OPEN_AI => "OpenAI",
        Providers.ANTHROPIC => "Anthropic",
        Providers.MISTRAL => "Mistral",
        
        Providers.FIREWORKS => "Fireworks.ai",
        
        Providers.SELF_HOSTED => "Self-hosted",
        
        _ => "Unknown",
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
            return providerSettings.UsedProvider switch
            {
                Providers.OPEN_AI => new ProviderOpenAI(logger) { InstanceName = providerSettings.InstanceName },
                Providers.ANTHROPIC => new ProviderAnthropic(logger) { InstanceName = providerSettings.InstanceName },
                Providers.MISTRAL => new ProviderMistral(logger) { InstanceName = providerSettings.InstanceName },
                
                Providers.FIREWORKS => new ProviderFireworks(logger) { InstanceName = providerSettings.InstanceName },
                
                Providers.SELF_HOSTED => new ProviderSelfHosted(logger, providerSettings) { InstanceName = providerSettings.InstanceName },
                
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