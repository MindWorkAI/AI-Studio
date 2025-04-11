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
    GOOGLE = 7,
    X = 8,
    DEEP_SEEK = 11,
    ALIBABA_CLOUD = 12,
    
    FIREWORKS = 5,
    GROQ = 6,
    HUGGINGFACE = 13,
    
    SELF_HOSTED = 4,
    
    HELMHOLTZ = 9,
    GWDG = 10,
}