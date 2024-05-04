using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider;

/// <summary>
/// Enum for all available providers.
/// </summary>
public enum Providers
{
    NONE,
    OPEN_AI,
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
        Providers.OPEN_AI => "OpenAI",
        
        _ => "Unknown",
    };
    
    /// <summary>
    /// Creates a new provider instance based on the provider value.
    /// </summary>
    /// <param name="provider">The provider value.</param>
    /// <returns>The provider instance.</returns>
    public static IProvider CreateProvider(this Providers provider) => provider switch
    {
        Providers.OPEN_AI => new ProviderOpenAI(),
        
        _ => new NoProvider(),
    };
}