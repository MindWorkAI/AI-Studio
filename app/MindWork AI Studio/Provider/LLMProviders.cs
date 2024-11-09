namespace AIStudio.Provider;

/// <summary>
/// Enum for all available providers.
/// </summary>
public enum LLMProviders
{
    NONE = 0,
    
    OPEN_AI = 1,
    ANTHROPIC = 2,
    MISTRAL = 3,
    
    FIREWORKS = 5,
    GROQ = 6,
    
    SELF_HOSTED = 4,
}