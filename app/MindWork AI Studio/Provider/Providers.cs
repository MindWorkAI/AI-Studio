using AIStudio.Provider.Anthropic;
using AIStudio.Provider.Mistral;
using AIStudio.Provider.OpenAI;
using AIStudio.Provider.SelfHosted;

namespace AIStudio.Provider;

/// <summary>
/// Enum for all available providers.
/// </summary>
public enum Providers
{
    NONE,
    
    OPEN_AI,
    ANTHROPIC,
    MISTRAL,
    
    SELF_HOSTED,
}

/// <summary>
/// Extension methods for the provider enum.
/// </summary>
public static class ExtensionsProvider
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
        
        Providers.SELF_HOSTED => "Self-hosted",
        
        _ => "Unknown",
    };

    /// <summary>
    /// Creates a new provider instance based on the provider value.
    /// </summary>
    /// <param name="providerSettings">The provider settings.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this Settings.Provider providerSettings) => providerSettings.UsedProvider switch
    {
        Providers.OPEN_AI => new ProviderOpenAI { InstanceName = providerSettings.InstanceName },
        Providers.ANTHROPIC => new ProviderAnthropic { InstanceName = providerSettings.InstanceName },
        Providers.MISTRAL => new ProviderMistral { InstanceName = providerSettings.InstanceName },
        
        Providers.SELF_HOSTED => new ProviderSelfHosted(providerSettings) { InstanceName = providerSettings.InstanceName },
        
        _ => new NoProvider(),
    };
}