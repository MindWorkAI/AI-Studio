using AIStudio.Provider.OpenAI;

namespace AIStudio.Provider;

public enum Providers
{
    NONE,
    OPEN_AI,
}

public static class ExtensionsProvider
{
    public static string ToName(this Providers provider) => provider switch
    {
        Providers.OPEN_AI => "OpenAI",
        
        _ => "Unknown",
    };
    
    public static IProvider CreateProvider(this Providers provider) => provider switch
    {
        Providers.OPEN_AI => new ProviderOpenAI(),
        
        _ => new NoProvider(),
    };
}