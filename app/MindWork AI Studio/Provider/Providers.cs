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
    /// <param name="provider">The provider value.</param>
    /// <param name="instanceName">The used instance name.</param>
    /// <param name="hostname">The hostname of the provider.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this Providers provider, string instanceName, string hostname = "http://localhost:1234") => provider switch
    {
        Providers.OPEN_AI => new ProviderOpenAI { InstanceName = instanceName },
        Providers.ANTHROPIC => new ProviderAnthropic { InstanceName = instanceName },
        Providers.MISTRAL => new ProviderMistral { InstanceName = instanceName },
        
        Providers.SELF_HOSTED => new ProviderSelfHosted(hostname) { InstanceName = instanceName },
        
        _ => new NoProvider(),
    };
}